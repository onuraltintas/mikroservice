using FluentAssertions;
using Identity.Domain.Entities;

namespace Identity.API.IntegrationTests;

public sealed class UserAccessLifecycleTests
{
    [Fact]
    public void ResetMfa_ShouldRemoveEveryMfaCredentialAndLockState()
    {
        var user = User.Create(Guid.NewGuid(), "admin@example.com", "Admin", "User");
        user.EnableMfa("protected-secret", ["recovery-hash"], DateTimeOffset.UtcNow);
        user.TryAcceptMfaTimeStep(42).Should().BeTrue();
        user.RecordFailedMfaAttempt(DateTimeOffset.UtcNow);

        user.ResetMfa();

        user.MfaEnabled.Should().BeFalse();
        user.MfaSecretProtected.Should().BeNull();
        user.MfaRecoveryCodeHashes.Should().BeEmpty();
        user.MfaEnabledAt.Should().BeNull();
        user.LastAcceptedMfaTimeStep.Should().BeNull();
        user.MfaFailedAttempts.Should().Be(0);
        user.MfaLockedUntil.Should().BeNull();
    }

    [Fact]
    public void Revoke_ShouldBeIdempotentAndPreserveOriginalAuditReason()
    {
        var token = RefreshToken.Create(
            Guid.NewGuid(), "refresh-token", DateTime.UtcNow.AddHours(1), "127.0.0.1");

        token.Revoke("10.0.0.1", "Administrator terminated the session");
        var revokedAt = token.RevokedAt;
        token.Revoke("10.0.0.2", "Second revocation");

        token.RevokedAt.Should().Be(revokedAt);
        token.RevokedByIp.Should().Be("10.0.0.1");
        token.ReasonRevoked.Should().Be("Administrator terminated the session");
    }
}
