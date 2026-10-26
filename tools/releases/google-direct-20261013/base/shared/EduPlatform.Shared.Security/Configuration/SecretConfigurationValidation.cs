using System.Text;
using Microsoft.Extensions.Configuration;

namespace EduPlatform.Shared.Security.Configuration;

/// <summary>
/// Shared fail-fast checks for credentials that must be supplied by deployment configuration.
/// The validator never includes the configured value in an exception message.
/// </summary>
public static class SecretConfigurationValidation
{
    public const int MinimumSecretLength = 32;

    public static void Validate(string? value, string settingName, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(value)
            || Encoding.UTF8.GetByteCount(value) < MinimumSecretLength)
        {
            throw new InvalidOperationException(
                $"{settingName} must be configured with at least {MinimumSecretLength} UTF-8 bytes.");
        }

        if (IsProduction(configuration) && IsKnownPlaceholder(value))
        {
            throw new InvalidOperationException(
                $"{settingName} contains a known placeholder and cannot be used in Production.");
        }
    }

    public static bool IsProduction(IConfiguration configuration)
    {
        var environment = new[]
        {
            configuration["ASPNETCORE_ENVIRONMENT"],
            configuration["DOTNET_ENVIRONMENT"],
            configuration["ENVIRONMENT"]
        }.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsKnownPlaceholder(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();

        return normalized.StartsWith("replace-with-", StringComparison.Ordinal)
            || normalized.StartsWith("change-me", StringComparison.Ordinal)
            || normalized.Contains("your-secret", StringComparison.Ordinal)
            || normalized.Contains("your-key", StringComparison.Ordinal)
            || normalized.Contains("example-secret", StringComparison.Ordinal)
            || normalized.Contains("example-key", StringComparison.Ordinal);
    }
}
