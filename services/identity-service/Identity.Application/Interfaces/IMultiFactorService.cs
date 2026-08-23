using EduPlatform.Shared.Kernel.Results;

namespace Identity.Application.Interfaces;

public interface IMultiFactorService
{
    string CreateChallenge(Guid userId, bool rememberMe);
    Result<MfaChallengePayload> ReadChallenge(string token);
    MfaSetupResponse CreateSetup(Guid userId, string email);
    Result<MfaSetupPayload> ReadSetupToken(string token);
    string ProtectSecret(string secret);
    string UnprotectSecret(string protectedSecret);
    long? FindMatchingTimeStep(string protectedSecret, string code);
    IReadOnlyList<string> GenerateRecoveryCodes();
    string HashRecoveryCode(string recoveryCode);
}

public interface IMfaExpiringPayload
{
    DateTimeOffset ExpiresAt { get; }
}

public sealed record MfaChallengePayload(
    Guid UserId,
    bool RememberMe,
    DateTimeOffset ExpiresAt) : IMfaExpiringPayload;

public sealed record MfaSetupPayload(
    Guid UserId,
    string Secret,
    DateTimeOffset ExpiresAt) : IMfaExpiringPayload;

public sealed record MfaSetupResponse(
    string Secret,
    string OtpAuthUri,
    string SetupToken,
    string? ChallengeToken = null);
