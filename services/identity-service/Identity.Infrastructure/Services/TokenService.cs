using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EduPlatform.Shared.Security.Configuration;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly IConfigurationService _configService;

    public TokenService(IConfiguration configuration, IConfigurationService configService)
    {
        _configuration = configuration;
        _configService = configService;
    }

    public string GenerateAccessToken(User user)
    {
        try 
        {
            // 1. Try to get dynamic configuration from Redis/DB
            var dynamicExpiryStr = _configService.GetConfigurationValueAsync("Auth.TokenLifetime", CancellationToken.None).GetAwaiter().GetResult();
            int.TryParse(dynamicExpiryStr, out var dynamicExpiry);

            // 2. Fallback to Environment Variables or appsettings
            var keyRing = JwtKeyRing.FromConfiguration(_configuration);
            
            var issuer = _configuration["JWT_ISSUER"]
                        ?? _configuration["Jwt:Issuer"]
                        ?? throw new InvalidOperationException("JWT_ISSUER is not configured.");
            var audience = _configuration["JWT_AUDIENCE"]
                          ?? _configuration["Jwt:Audience"]
                          ?? throw new InvalidOperationException("JWT_AUDIENCE is not configured.");
            
            int expiryMinutes;
            if (dynamicExpiry > 0)
            {
                expiryMinutes = dynamicExpiry;
            }
            else 
            {
                var expiryMinutesStr = Environment.GetEnvironmentVariable("JWT_EXPIRY_MINUTES") 
                                      ?? _configuration["JWT_EXPIRY_MINUTES"]
                                      ?? "30";
                int.TryParse(expiryMinutesStr, out expiryMinutes);
                if (expiryMinutes == 0) expiryMinutes = 30;
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyRing.ActiveSecret))
            {
                KeyId = keyRing.ActiveKeyId
            };
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

            if (!string.IsNullOrWhiteSpace(keyRing.ActiveKeyId))
            {
                token.Header["kid"] = keyRing.ActiveKeyId;
            }

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("JWT generation failed.", ex);
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
