using FluentAssertions;
using Identity.Infrastructure.Security;

namespace Identity.API.IntegrationTests;

public sealed class TotpServiceTests
{
    [Fact]
    public void GenerateSecret_ShouldCreateA160BitBase32Secret()
    {
        var secret = TotpService.GenerateSecret();

        secret.Should().HaveLength(32);
        secret.Should().MatchRegex("^[A-Z2-7]+$");
        TotpService.DecodeSecret(secret).Should().HaveCount(20);
    }

    [Fact]
    public void GenerateCode_ShouldMatchRfc6238Sha1Vector()
    {
        var secret = "12345678901234567890"u8.ToArray();
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(59);

        var code = TotpService.GenerateCode(secret, timestamp, digits: 8);

        code.Should().Be("94287082");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void FindMatchingTimeStep_ShouldAcceptOnlyTheConfiguredClockWindow(int drift)
    {
        var secret = "12345678901234567890"u8.ToArray();
        var now = DateTimeOffset.FromUnixTimeSeconds(90);
        var code = TotpService.GenerateCode(secret, now.AddSeconds(drift * 30));

        var step = TotpService.FindMatchingTimeStep(secret, code, now, allowedDriftWindows: 1);

        step.Should().Be((now.ToUnixTimeSeconds() / 30) + drift);
    }

    [Fact]
    public void FindMatchingTimeStep_ShouldRejectMalformedAndOutOfWindowCodes()
    {
        var secret = "12345678901234567890"u8.ToArray();
        var now = DateTimeOffset.FromUnixTimeSeconds(90);
        var oldCode = TotpService.GenerateCode(secret, now.AddSeconds(-60));

        TotpService.FindMatchingTimeStep(secret, "12A456", now).Should().BeNull();
        TotpService.FindMatchingTimeStep(secret, oldCode, now).Should().BeNull();
    }
}
