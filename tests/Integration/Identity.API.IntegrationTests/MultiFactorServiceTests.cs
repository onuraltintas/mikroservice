using FluentAssertions;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;

namespace Identity.API.IntegrationTests;

public sealed class MultiFactorServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-19T10:00:00Z");

    [Fact]
    public void Challenge_ShouldRoundTripFirstFactorContext()
    {
        var userId = Guid.NewGuid();
        var service = CreateService(Now);

        var token = service.CreateChallenge(userId, rememberMe: false);
        var result = service.ReadChallenge(token);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.RememberMe.Should().BeFalse();
    }

    [Fact]
    public void Challenge_ShouldRejectTamperingAndExpiry()
    {
        var clock = new TestTimeProvider(Now);
        var service = CreateService(clock);
        var token = service.CreateChallenge(Guid.NewGuid(), rememberMe: true);

        service.ReadChallenge(token + "tampered").IsFailure.Should().BeTrue();

        clock.Advance(TimeSpan.FromMinutes(6));
        service.ReadChallenge(token).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetupToken_ShouldKeepRawSecretOutOfPersistentState()
    {
        var service = CreateService(Now);
        var setup = service.CreateSetup(Guid.NewGuid(), "admin@example.com");

        setup.Secret.Should().MatchRegex("^[A-Z2-7]{32}$");
        setup.OtpAuthUri.Should().StartWith("otpauth://totp/EduPlatform:");
        setup.SetupToken.Should().NotContain(setup.Secret);

        var result = service.ReadSetupToken(setup.SetupToken);
        result.IsSuccess.Should().BeTrue();
        result.Value.Secret.Should().Be(setup.Secret);
    }

    [Fact]
    public void RecoveryCodes_ShouldBeRandomAndStoredAsOneWayHashes()
    {
        var service = CreateService(Now);

        var codes = service.GenerateRecoveryCodes();

        codes.Should().HaveCount(10).And.OnlyHaveUniqueItems();
        codes.Should().OnlyContain(code => code.Length == 14 && code[6] == '-');
        service.HashRecoveryCode(codes[0]).Should().NotContain(codes[0]);
        service.HashRecoveryCode(codes[0].ToLowerInvariant())
            .Should().Be(service.HashRecoveryCode(codes[0]));
    }

    private static MultiFactorService CreateService(DateTimeOffset now) =>
        CreateService(new TestTimeProvider(now));

    private static MultiFactorService CreateService(TimeProvider timeProvider) =>
        new(new EphemeralDataProtectionProvider(), timeProvider);

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
