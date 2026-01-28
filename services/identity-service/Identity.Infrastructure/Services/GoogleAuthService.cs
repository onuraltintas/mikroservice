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
            var clientId = _configuration["GOOGLE_CLIENT_ID"];
            GoogleJsonWebSignature.ValidationSettings? settings = null;

            if (!string.IsNullOrEmpty(clientId))
            {
                settings = new GoogleJsonWebSignature.ValidationSettings()
                {
                    Audience = new List<string>() { clientId }
                };
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
