using FluentAssertions;
using SpeedReading.Application.AdaptiveLearning;

namespace SpeedReading.Application.UnitTests;

public sealed class AdaptiveProgressionRulesTests
{
    private static readonly AdaptiveProgressionPolicy DefaultPolicy = new(
        MinimumMeasuredSessions: 3,
        AdvanceComprehensionThreshold: 80,
        MaintainComprehensionThreshold: 65,
        MinimumWpmTrendPercent: 0,
        SupportTrendPercent: -10);

    [Fact]
    public void Collects_more_evidence_until_the_minimum_measured_session_count_is_reached()
    {
        var decision = AdaptiveProgressionRules.Evaluate(
            [
                new AdaptiveProgressionEvidence(210, 84),
                new AdaptiveProgressionEvidence(220, 82)
            ],
            DefaultPolicy);

        decision.Kind.Should().Be(AdaptiveProgressionDecisionKind.CollectEvidence);
        decision.DifficultyAdjustment.Should().Be(0);
    }

    [Fact]
    public void Advances_only_when_comprehension_is_preserved_and_speed_is_not_declining()
    {
        var decision = AdaptiveProgressionRules.Evaluate(
            [
                new AdaptiveProgressionEvidence(200, 82),
                new AdaptiveProgressionEvidence(210, 84),
                new AdaptiveProgressionEvidence(220, 81)
            ],
            DefaultPolicy);

        decision.Kind.Should().Be(AdaptiveProgressionDecisionKind.Advance);
        decision.DifficultyAdjustment.Should().Be(1);
    }

    [Fact]
    public void Maintains_difficulty_when_comprehension_is_between_support_and_advance_thresholds()
    {
        var decision = AdaptiveProgressionRules.Evaluate(
            [
                new AdaptiveProgressionEvidence(200, 72),
                new AdaptiveProgressionEvidence(205, 71),
                new AdaptiveProgressionEvidence(208, 73)
            ],
            DefaultPolicy);

        decision.Kind.Should().Be(AdaptiveProgressionDecisionKind.Maintain);
        decision.DifficultyAdjustment.Should().Be(0);
    }

    [Fact]
    public void Assigns_support_when_comprehension_falls_below_the_support_threshold()
    {
        var decision = AdaptiveProgressionRules.Evaluate(
            [
                new AdaptiveProgressionEvidence(220, 78),
                new AdaptiveProgressionEvidence(215, 62),
                new AdaptiveProgressionEvidence(205, 61)
            ],
            DefaultPolicy);

        decision.Kind.Should().Be(AdaptiveProgressionDecisionKind.Support);
        decision.DifficultyAdjustment.Should().Be(-1);
    }

    [Fact]
    public void Does_not_advance_when_speed_improves_but_comprehension_drops()
    {
        var decision = AdaptiveProgressionRules.Evaluate(
            [
                new AdaptiveProgressionEvidence(200, 84),
                new AdaptiveProgressionEvidence(230, 83),
                new AdaptiveProgressionEvidence(260, 60)
            ],
            DefaultPolicy);

        decision.Kind.Should().Be(AdaptiveProgressionDecisionKind.Support);
    }

    [Fact]
    public void Assigns_support_when_speed_trend_is_a_material_decline()
    {
        var decision = AdaptiveProgressionRules.Evaluate(
            [
                new AdaptiveProgressionEvidence(240, 85),
                new AdaptiveProgressionEvidence(220, 84),
                new AdaptiveProgressionEvidence(200, 83)
            ],
            DefaultPolicy);

        decision.Kind.Should().Be(AdaptiveProgressionDecisionKind.Support);
        decision.DifficultyAdjustment.Should().Be(-1);
    }
}
