using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace EduPlatform.Shared.Security.Services;

/// <summary>
/// Validates the shared key used for service-to-service HTTP calls.
/// The key must be supplied by deployment configuration; there is no fallback value.
/// </summary>
public static class InternalServiceAuthentication
{
    public const string HeaderName = "X-Internal-Service-Key";
    public const int MinimumKeyLength = 32;

    public static void ValidateConfiguration(IConfiguration configuration)
    {
        var configuredKey = configuration["INTERNAL_SERVICE_API_KEY"]
            ?? configuration["Internal:ServiceApiKey"];

        if (string.IsNullOrWhiteSpace(configuredKey)
            || Encoding.UTF8.GetByteCount(configuredKey) < MinimumKeyLength)
        {
            throw new InvalidOperationException(
                $"{HeaderName} must be configured with at least {MinimumKeyLength} UTF-8 bytes.");
        }
    }

    public static bool IsValid(HttpRequest request, IConfiguration configuration)
    {
        var expectedKey = configuration["INTERNAL_SERVICE_API_KEY"]
            ?? configuration["Internal:ServiceApiKey"];

        if (string.IsNullOrWhiteSpace(expectedKey)
            || Encoding.UTF8.GetByteCount(expectedKey) < MinimumKeyLength
            || !request.Headers.TryGetValue(HeaderName, out var providedHeader))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedHeader.ToString());

        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
