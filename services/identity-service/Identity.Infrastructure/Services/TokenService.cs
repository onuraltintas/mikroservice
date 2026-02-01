using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateAccessToken(User user)
    {
        try 
        {
            // Check environment variables first, then fallback to configuration
            var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET") 
                            ?? _configuration["JWT_SECRET"] 
                            ?? throw new InvalidOperationException("JWT_SECRET is not configured.");
            
            var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") 
                        ?? _configuration["JWT_ISSUER"] 
                        ?? throw new InvalidOperationException("JWT_ISSUER is not configured.");
            var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") 
                          ?? _configuration["JWT_AUDIENCE"] 
                          ?? throw new InvalidOperationException("JWT_AUDIENCE is not configured.");
            var expiryMinutesStr = Environment.GetEnvironmentVariable("JWT_EXPIRY_MINUTES") 
                                  ?? _configuration["JWT_EXPIRY_MINUTES"]
                                  ?? throw new InvalidOperationException("JWT_EXPIRY_MINUTES is not configured.");
            int.TryParse(expiryMinutesStr, out var expiryMinutes);
            if (expiryMinutes == 0) expiryMinutes = 30; // Safety fallback for parse error

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (user.Roles != null)
            {
                foreach (var userRole in user.Roles)
                {
                    // Skip deleted roles
                    if (userRole.Role != null && !userRole.Role.IsDeleted)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, userRole.Role.Name));

                        if (userRole.Role.Permissions != null)
                        {
                            foreach (var perm in userRole.Role.Permissions)
                            {
                                if (!claims.Any(c => c.Type == "permission" && c.Value == perm.Permission))
                                {
                                    claims.Add(new Claim("permission", perm.Permission));
                                }
                            }
                        }
                    }
                }
            }

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        catch (Exception ex)
        {
            throw new Exception($"JWT Generation Failed: {ex.Message} - Stack: {ex.StackTrace}", ex);
        }
    }

    public RefreshToken GenerateRefreshToken(Guid userId, string ipAddress)
    {
        var expiryDaysStr = Environment.GetEnvironmentVariable("JWT_REFRESH_TOKEN_EXPIRY_DAYS")
                            ?? _configuration["JWT_REFRESH_TOKEN_EXPIRY_DAYS"]
                            ?? "7"; // Default 7 days
        
        if (!int.TryParse(expiryDaysStr, out var expiryDays)) expiryDays = 7;
        
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        var token = Convert.ToBase64String(randomNumber);

        return RefreshToken.Create(
            userId, 
            token, 
            DateTime.UtcNow.AddDays(expiryDays), 
            ipAddress
        );
    }
}
