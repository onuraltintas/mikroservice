using FluentAssertions;
using SpeedReading.Application.Content;

namespace SpeedReading.Application.UnitTests;

public sealed class EvidenceMetricRulesTests
{
    [Theory]
    [InlineData("{\"isVisible\":true}", true)]
    [InlineData("{\"isVisible\":false}", false)]
    [InlineData("{}", false)]
    [InlineData("not-json", false)]
    public void Exposes_only_explicitly_visible_evidence_metrics(string value, bool expected)
    {
        EvidenceMetricRules.IsPubliclyVisible(value).Should().Be(expected);
    }

    [Fact]
    public void Hides_numeric_claims_until_the_source_is_marked_verified()
    {
        EvidenceMetricRules.IsPubliclyVisible("{\"isVisible\":true,\"value\":\"250000+\"}").Should().BeFalse();
        EvidenceMetricRules.IsPubliclyVisible("{\"isVisible\":true,\"value\":\"250000+\",\"verified\":true}").Should().BeTrue();
    }

    [Fact]
    public void Hides_numeric_json_values_even_when_they_are_not_marked_as_claim_text()
    {
        EvidenceMetricRules.IsPubliclyVisible("{\"isVisible\":true,\"value\":250000}").Should().BeFalse();
    }
}
