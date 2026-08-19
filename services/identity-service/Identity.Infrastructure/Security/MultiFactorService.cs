using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace Identity.Infrastructure.Security;

public sealed class MultiFactorService : IMultiFactorService
{
    private const string Issuer = "EduPlatform";
    private const string RecoveryAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SetupLifetime = TimeSpan.FromMinutes(10);

    private readonly IDataProtector _challengeProtector;
    private readonly IDataProtector _setupProtector;
    private readonly IDataProtector _secretProtector;
    private readonly TimeProvider _timeProvider;

    public MultiFactorService(IDataProtectionProvider dataProtectionProvider, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _challengeProtector = dataProtectionProvider.CreateProtector("Identity.Mfa.Challenge.v1");
        _setupProtector = dataProtectionProvider.CreateProtector("Identity.Mfa.Setup.v1");
        _secretProtector = dataProtectionProvider.CreateProtector("Identity.Mfa.Secret.v1");
    }

    public string CreateChallenge(Guid userId, bool rememberMe)
    {
        var payload = new MfaChallengePayload(
            userId,
            rememberMe,
            _timeProvider.GetUtcNow().Add(ChallengeLifetime));
        return Protect(_challengeProtector, payload);
    }

    public Result<MfaChallengePayload> ReadChallenge(string token) =>
        Read<MfaChallengePayload>(_challengeProtector, token, "Auth.InvalidMfaChallenge");

    public MfaSetupResponse CreateSetup(Guid userId, string email)
    {
        var secret = TotpService.GenerateSecret();
        var payload = new MfaSetupPayload(userId, secret, _timeProvider.GetUtcNow().Add(SetupLifetime));
        var label = $"{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(email)}";
        var issuer = Uri.EscapeDataString(Issuer);
        var uri = $"otpauth://totp/{label}?secret={secret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";
        return new MfaSetupResponse(secret, uri, Protect(_setupProtector, payload));
    }

    public Result<MfaSetupPayload> ReadSetupToken(string token) =>
        Read<MfaSetupPayload>(_setupProtector, token, "Auth.InvalidMfaSetup");

    public string ProtectSecret(string secret) => _secretProtector.Protect(secret);

    public string UnprotectSecret(string protectedSecret) => _secretProtector.Unprotect(protectedSecret);

    public long? FindMatchingTimeStep(string protectedSecret, string code)
    {
        try
        {
            var secret = TotpService.DecodeSecret(UnprotectSecret(protectedSecret));
            return TotpService.FindMatchingTimeStep(secret, code, _timeProvider.GetUtcNow());
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return null;
        }
    }

    public IReadOnlyList<string> GenerateRecoveryCodes()
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        while (codes.Count < 10)
        {
            var random = new byte[13];
            RandomNumberGenerator.Fill(random);
            var characters = new char[14];
            for (var index = 0; index < 13; index++)
            {
                var outputIndex = index < 6 ? index : index + 1;
                characters[outputIndex] = RecoveryAlphabet[random[index] % RecoveryAlphabet.Length];
            }

            characters[6] = '-';
            codes.Add(new string(characters));
        }

        return codes.ToArray();
    }

    public string HashRecoveryCode(string recoveryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryCode);
        var normalized = recoveryCode.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private string Protect<T>(IDataProtector protector, T payload) =>
        protector.Protect(JsonSerializer.Serialize(payload));

    private Result<T> Read<T>(IDataProtector protector, string token, string errorCode)
        where T : IMfaExpiringPayload
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Invalid<T>(errorCode);
        }

        try
        {
            var payload = JsonSerializer.Deserialize<T>(protector.Unprotect(token));
            return payload is not null && payload.ExpiresAt > _timeProvider.GetUtcNow()
                ? Result.Success(payload)
                : Invalid<T>(errorCode);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            return Invalid<T>(errorCode);
        }
    }

    private static Result<T> Invalid<T>(string errorCode) =>
        Result.Failure<T>(new Error(errorCode, "MFA doğrulama isteği geçersiz veya süresi dolmuş."));
}
