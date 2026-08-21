using Google.Apis.Auth;
using Identity.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private const int MaxIdTokenLength = 16_384;
    private static readonly string[] TrustedIssuers =
    [
        "https://accounts.google.com",
        "accounts.google.com"
    ];

    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(IConfiguration configuration, ILogger<GoogleAuthService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<GoogleUser?> VerifyGoogleTokenAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken) || idToken.Length > MaxIdTokenLength)
        {
            return null;
        }

        try
        {
            var clientId = _configuration["GOOGLE_CLIENT_ID"]
                           ?? _configuration["Authentication:Google:ClientId"];
            if (string.IsNullOrWhiteSpace(clientId))
            {
                _logger.LogError("Google authentication is not configured; refusing to validate an ID token.");
                return null;
            }

            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new List<string> { clientId }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            if (string.IsNullOrWhiteSpace(payload.Subject)
                || string.IsNullOrWhiteSpace(payload.Email)
                || !payload.EmailVerified
                || !TrustedIssuers.Contains(payload.Issuer, StringComparer.Ordinal))
            {
                _logger.LogWarning("Google ID token claims did not satisfy the required security checks.");
                return null;
            }

            return new GoogleUser(
                payload.Email,
                payload.GivenName ?? "",
                payload.FamilyName ?? "",
                payload.Picture,
                payload.Subject,
                payload.EmailVerified,
                payload.Issuer!
            );
        }
        catch (InvalidJwtException)
        {
            _logger.LogWarning("Invalid Google ID token.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError("Google ID token verification failed: {ExceptionType}.", ex.GetType().Name);
            return null;
        }
    }
}
