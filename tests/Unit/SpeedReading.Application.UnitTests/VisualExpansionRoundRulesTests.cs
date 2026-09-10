using FluentAssertions;
using SpeedReading.Application.ExerciseSessions;

namespace SpeedReading.Application.UnitTests;

public sealed class VisualExpansionRoundRulesTests
{
    [Fact]
    public void Answers_are_compared_without_case_or_surrounding_whitespace()
    {
        VisualExpansionRoundRules.Evaluate(
            ["A", "7"],
            [" a ", "7"],
            responseTimeMs: 640,
            minimumResponseTimeMs: 100,
            maximumResponseTimeMs: 5_000)
            .Should().Be(new VisualExpansionRoundResult(true, true, 640));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(5001)]
    public void Implausible_response_time_is_rejected(int responseTimeMs)
    {
        VisualExpansionRoundRules.Evaluate(
            ["K", "3"],
            ["K", "3"],
            responseTimeMs,
            minimumResponseTimeMs: 100,
            maximumResponseTimeMs: 5_000)
            .Should().Be(new VisualExpansionRoundResult(false, false, responseTimeMs));
    }

    [Fact]
    public void Missing_or_extra_answers_are_incorrect()
    {
        VisualExpansionRoundRules.Evaluate(
            ["B", "D"],
            ["B"],
            responseTimeMs: 800,
            minimumResponseTimeMs: 100,
            maximumResponseTimeMs: 5_000)
            .Should().Be(new VisualExpansionRoundResult(true, false, 800));

        VisualExpansionRoundRules.Evaluate(
            ["B", "D"],
            ["B", "D", "X"],
            responseTimeMs: 800,
            minimumResponseTimeMs: 100,
            maximumResponseTimeMs: 5_000)
            .Should().Be(new VisualExpansionRoundResult(true, false, 800));
    }
}
