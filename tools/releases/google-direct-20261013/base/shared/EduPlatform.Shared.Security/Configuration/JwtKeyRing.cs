using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace EduPlatform.Shared.Security.Configuration;

public sealed record JwtValidationKey(string Secret, string? KeyId);

/// <summary>
/// Holds the active JWT signing secret and the short-lived previous-secret overlap
/// used during rotation. Previous secrets are validation-only and are never used to sign.
/// </summary>
public sealed class JwtKeyRing
{
    public const string ActiveSecretKey = "JWT_SECRET";
    public const string PreviousSecretsKey = "JWT_PREVIOUS_SECRETS";
    public const string ActiveKeyIdKey = "JWT_KEY_ID";
    public const string PreviousKeyIdsKey = "JWT_PREVIOUS_KEY_IDS";

    private JwtKeyRing(
        string activeSecret,
        string? activeKeyId,
        IReadOnlyList<JwtValidationKey> validationKeys)
    {
        ActiveSecret = activeSecret;
        ActiveKeyId = activeKeyId;
        ValidationKeys = validationKeys;
    }

    public string ActiveSecret { get; }

    public string? ActiveKeyId { get; }

    public IReadOnlyList<JwtValidationKey> ValidationKeys { get; }

    public IReadOnlyList<SecurityKey> CreateSecurityKeys()
    {
        return ValidationKeys
            .Select(key =>
            {
                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key.Secret));
                securityKey.KeyId = key.KeyId;
                return (SecurityKey)securityKey;
            })
            .ToArray();
    }

    public static JwtKeyRing FromConfiguration(IConfiguration configuration)
    {
        var activeSecret = GetRequired(configuration, ActiveSecretKey, "Jwt:Secret");
        SecretConfigurationValidation.Validate(activeSecret, ActiveSecretKey, configuration);

        var activeKeyId = GetOptional(configuration, ActiveKeyIdKey, "Jwt:KeyId");
        ValidateKeyId(activeKeyId, ActiveKeyIdKey);

        var previousSecrets = ParseList(
            GetOptional(configuration, PreviousSecretsKey, "Jwt:PreviousSecrets"));
        var previousKeyIds = ParseList(
            GetOptional(configuration, PreviousKeyIdsKey, "Jwt:PreviousKeyIds"));

        if (previousKeyIds.Count != 0 && previousKeyIds.Count != previousSecrets.Count)
        {
            throw new InvalidOperationException(
                $"{PreviousKeyIdsKey} must contain the same number of entries as {PreviousSecretsKey}.");
        }

        if (!string.IsNullOrWhiteSpace(activeKeyId)
            && previousSecrets.Count != 0
            && previousKeyIds.Count == 0)
        {
            throw new InvalidOperationException(
                $"{PreviousKeyIdsKey} must contain one key id for each previous secret when {ActiveKeyIdKey} is configured.");
        }

        var validationKeys = new List<JwtValidationKey>
        {
            new(activeSecret, activeKeyId)
        };

        var seenSecrets = new HashSet<string>(StringComparer.Ordinal)
        {
            activeSecret
        };
        var seenKeyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(activeKeyId))
        {
            seenKeyIds.Add(activeKeyId);
        }

        for (var index = 0; index < previousSecrets.Count; index++)
        {
            var previousSecret = previousSecrets[index];
            SecretConfigurationValidation.Validate(
                previousSecret,
                $"{PreviousSecretsKey}[{index}]",
                configuration);

            if (!seenSecrets.Add(previousSecret))
            {
                throw new InvalidOperationException(
                    $"{PreviousSecretsKey} must not contain duplicate or active secrets.");
            }

            var previousKeyId = previousKeyIds.Count == 0 ? null : previousKeyIds[index];
            ValidateKeyId(previousKeyId, $"{PreviousKeyIdsKey}[{index}]");
            if (!string.IsNullOrWhiteSpace(previousKeyId) && !seenKeyIds.Add(previousKeyId))
            {
                throw new InvalidOperationException(
                    "JWT key identifiers must be unique across active and previous keys.");
            }

            validationKeys.Add(new JwtValidationKey(previousSecret, previousKeyId));
        }

        return new JwtKeyRing(activeSecret, activeKeyId, validationKeys);
    }

    private static string GetRequired(
        IConfiguration configuration,
        string environmentKey,
        string nestedKey)
    {
        return configuration[environmentKey]
            ?? configuration[nestedKey]
            ?? throw new InvalidOperationException($"{environmentKey} is not configured.");
    }

    private static string? GetOptional(
        IConfiguration configuration,
        string environmentKey,
        string nestedKey)
    {
        return configuration[environmentKey] ?? configuration[nestedKey];
    }

    private static IReadOnlyList<string> ParseList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
    }

    private static void ValidateKeyId(string? keyId, string settingName)
    {
        if (string.IsNullOrWhiteSpace(keyId))
        {
            return;
        }

        if (keyId.Length > 64 || keyId.Any(character =>
                !(char.IsAsciiLetterOrDigit(character)
                  || character is '-' or '_' or '.' or ':')))
        {
            throw new InvalidOperationException(
                $"{settingName} must contain only ASCII letters, digits, '-', '_', '.', ':' and be at most 64 characters.");
        }
    }
}
