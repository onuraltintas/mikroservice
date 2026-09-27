using FluentAssertions;
using SpeedReading.Application.ExerciseSessions;

namespace SpeedReading.Application.UnitTests;

public sealed class VisualExpansionRoundRulesTests
{
    [Fact]
    public void Correct_answer_expands_angle_and_shortens_display_time()
    {
        VisualExpansionRoundRules.AdvanceDifficulty(
            currentDegrees: 4,
            targetDegrees: 30,
            currentDisplayDurationMs: 400,
            isCorrect: true)
            .Should().Be(new VisualExpansionDifficulty(6, 380));
    }

    [Fact]
    public void Incorrect_answer_keeps_angle_and_display_time_unchanged()
    {
        VisualExpansionRoundRules.AdvanceDifficulty(
            currentDegrees: 8,
            targetDegrees: 30,
            currentDisplayDurationMs: 300,
            isCorrect: false)
            .Should().Be(new VisualExpansionDifficulty(8, 300));
    }

    [Fact]
    public void Difficulty_progression_stops_at_safe_limits()
    {
        VisualExpansionRoundRules.AdvanceDifficulty(
            currentDegrees: 29,
            targetDegrees: 30,
            currentDisplayDurationMs: 105,
            isCorrect: true)
            .Should().Be(new VisualExpansionDifficulty(30, 100));
    }

    [Theory]
    [InlineData("letter")]
    [InlineData("number")]
    [InlineData("symbol")]
    public void Server_stimuli_are_deterministic_and_distinct(string stimulusType)
    {
        var first = VisualExpansionRoundRules.CreateStimuli(42, 3, stimulusType, 2);
        var retry = VisualExpansionRoundRules.CreateStimuli(42, 3, stimulusType, 2);

        first.Should().Equal(retry);
        first.Should().HaveCount(2).And.OnlyHaveUniqueItems();
        first.Should().OnlyContain(item => !string.IsNullOrWhiteSpace(item));
    }

    [Fact]
    public void Unsupported_stimulus_type_is_rejected()
    {
        var act = () => VisualExpansionRoundRules.CreateStimuli(42, 0, "html", 2);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

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
