using FluentAssertions;
using SpeedReading.Application.Content;

namespace SpeedReading.Application.UnitTests;

public sealed class PublicMarketingCopyRulesTests
{
    [Theory]
    [InlineData("Okuma hızınızı 3 katına çıkarın")]
    [InlineData("Başarı oranını 90% artırın")]
    [InlineData("Başarı oranını yüzde 90 artırın")]
    [InlineData("900+ WPM hedefi")]
    [InlineData("Kesin sonuç garanti")]
    public void Rejects_unsupported_outcome_claims(string text)
    {
        PublicMarketingCopyRules.IsSafe($"{{\"hero\":{{\"title\":\"{text}\"}}}}")
            .Should().BeFalse();
    }

    [Fact]
    public void Accepts_measured_language_and_rejects_malformed_json()
    {
        PublicMarketingCopyRules.IsSafe("{\"hero\":{\"title\":\"Hız ve anlamayı birlikte geliştirin\"}}").Should().BeTrue();
        PublicMarketingCopyRules.IsSafe("{\"hero\":{\"title\":\"Katmanlı çalışma akışı\"}}").Should().BeTrue();
        PublicMarketingCopyRules.IsSafe("not-json").Should().BeFalse();
    }

    [Fact]
    public void EnsureSafe_reports_an_actionable_business_rule()
    {
        var action = () => PublicMarketingCopyRules.EnsureSafe("{\"hero\":{\"title\":\"Kesin sonuç\"}}");

        action.Should().Throw<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>()
            .WithMessage("*doğrulanmamış*");
    }
}
