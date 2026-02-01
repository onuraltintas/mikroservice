using Google.Apis.Auth;
using Identity.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(IConfiguration configuration, ILogger<GoogleAuthService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<GoogleUser?> VerifyGoogleTokenAsync(string idToken)
    {
        try
        {
            // First check environment variable (from .env), then fallback to configuration
            var clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") 
                           ?? _configuration["GOOGLE_CLIENT_ID"];
            
            var settings = new GoogleJsonWebSignature.ValidationSettings();
            
            if (!string.IsNullOrEmpty(clientId))
            {
                settings.Audience = new List<string>() { clientId };
                _logger.LogDebug("Using GOOGLE_CLIENT_ID for token validation");
            }
            else
            {
                _logger.LogWarning("GOOGLE_CLIENT_ID is not configured. Token validation will proceed without audience check.");
            }

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            
            return new GoogleUser(
                payload.Email,
                payload.GivenName ?? "",
                payload.FamilyName ?? "",
                payload.Picture,
                payload.Subject // Google User ID
            );
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Invalid Google Token");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Auth Verification Failed");
            return null;
        }
    }
}
