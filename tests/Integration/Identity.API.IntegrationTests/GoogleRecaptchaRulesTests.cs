using FluentAssertions;
using SpeedReading.API.Security;
using System.Text.Json;
using Xunit;

namespace Identity.API.IntegrationTests;

public sealed class GoogleRecaptchaRulesTests
{
    private static readonly GoogleRecaptchaOptions EnabledOptions = new()
    {
        Enabled = true,
        SiteKey = "public-site-key",
        SecretKey = "server-secret-key",
        MinimumScore = 0.5m,
        AllowedHostnames = ["masterhizliokuma.com", "www.masterhizliokuma.com"]
    };

    [Fact]
    public void ValidContactVerification_ShouldBeAccepted()
    {
        var accepted = GoogleRecaptchaRules.IsAccepted(
            EnabledOptions,
            new GoogleRecaptchaVerification(true, 0.9m, GoogleRecaptchaRules.ContactAction, "masterhizliokuma.com"));

        accepted.Should().BeTrue();
    }

    [Theory]
    [InlineData(0.49)]
    [InlineData(0.1)]
    public void LowScoreVerification_ShouldBeRejected(decimal score)
    {
        var accepted = GoogleRecaptchaRules.IsAccepted(
            EnabledOptions,
            new GoogleRecaptchaVerification(true, score, GoogleRecaptchaRules.ContactAction, "masterhizliokuma.com"));

        accepted.Should().BeFalse();
    }

    [Fact]
    public void WrongActionOrHostname_ShouldBeRejected()
    {
        var wrongAction = GoogleRecaptchaRules.IsAccepted(
            EnabledOptions,
            new GoogleRecaptchaVerification(true, 0.9m, "registration", "masterhizliokuma.com"));
        var wrongHostname = GoogleRecaptchaRules.IsAccepted(
            EnabledOptions,
            new GoogleRecaptchaVerification(true, 0.9m, GoogleRecaptchaRules.ContactAction, "other.example"));

        wrongAction.Should().BeFalse();
        wrongHostname.Should().BeFalse();
    }

    [Fact]
    public void RejectedGoogleVerification_ShouldRetainErrorCodesForSafeServerDiagnostics()
    {
        var verification = JsonSerializer.Deserialize<GoogleRecaptchaVerification>(
            """{"success":false,"error-codes":["invalid-input-secret"]}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        verification!.ErrorCodes.Should().ContainSingle().Which.Should().Be("invalid-input-secret");
    }
}
