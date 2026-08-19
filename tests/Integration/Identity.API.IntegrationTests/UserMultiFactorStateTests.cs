using FluentAssertions;
using Identity.Domain.Entities;

namespace Identity.API.IntegrationTests;

public sealed class UserMultiFactorStateTests
{
    [Fact]
    public void EnableMfa_ShouldPersistProtectedSecretAndRecoveryCodeHashes()
    {
        var user = User.Create(Guid.NewGuid(), "admin@example.com");

        user.EnableMfa("protected-secret", ["hash-one", "hash-two"], DateTimeOffset.Parse("2026-08-19T10:00:00Z"));

        user.MfaEnabled.Should().BeTrue();
        user.MfaSecretProtected.Should().Be("protected-secret");
        user.MfaRecoveryCodeHashes.Should().BeEquivalentTo("hash-one", "hash-two");
        user.MfaEnabledAt.Should().Be(DateTimeOffset.Parse("2026-08-19T10:00:00Z"));
    }

    [Fact]
    public void AcceptMfaTimeStep_ShouldPreventTotpReplay()
    {
        var user = User.Create(Guid.NewGuid(), "admin@example.com");
        user.EnableMfa("protected-secret", ["hash-one"], DateTimeOffset.UtcNow);

        user.TryAcceptMfaTimeStep(42).Should().BeTrue();
        user.TryAcceptMfaTimeStep(42).Should().BeFalse();
        user.TryAcceptMfaTimeStep(41).Should().BeFalse();
        user.TryAcceptMfaTimeStep(43).Should().BeTrue();
    }

    [Fact]
    public void ConsumeRecoveryCode_ShouldBeSingleUse()
    {
        var user = User.Create(Guid.NewGuid(), "admin@example.com");
        user.EnableMfa("protected-secret", ["hash-one", "hash-two"], DateTimeOffset.UtcNow);

        user.ConsumeMfaRecoveryCode("hash-one").Should().BeTrue();
        user.ConsumeMfaRecoveryCode("hash-one").Should().BeFalse();
        user.MfaRecoveryCodeHashes.Should().ContainSingle().Which.Should().Be("hash-two");
    }

    [Fact]
    public void FailedAttempts_ShouldTemporarilyLockMfaVerification()
    {
        var user = User.Create(Guid.NewGuid(), "admin@example.com");
        var now = DateTimeOffset.Parse("2026-08-19T10:00:00Z");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            user.RecordFailedMfaAttempt(now);
        }

        user.IsMfaVerificationLocked(now.AddMinutes(4)).Should().BeTrue();
        user.IsMfaVerificationLocked(now.AddMinutes(5)).Should().BeFalse();

        user.ResetMfaFailures();
        user.MfaFailedAttempts.Should().Be(0);
        user.MfaLockedUntil.Should().BeNull();
    }

    [Fact]
    public void RefreshToken_ShouldCarryMfaAuthenticationTime()
    {
        var verifiedAt = DateTimeOffset.Parse("2026-08-19T10:00:00Z");

        var token = RefreshToken.Create(
            Guid.NewGuid(), "token", DateTime.UtcNow.AddDays(1), "127.0.0.1",
            isPersistent: true, mfaVerifiedAt: verifiedAt);

        token.MfaVerifiedAt.Should().Be(verifiedAt);
    }
}
