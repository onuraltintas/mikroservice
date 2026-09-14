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
}
