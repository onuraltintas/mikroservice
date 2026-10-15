using FluentAssertions;
using SpeedReading.Application.ExerciseSessions;

namespace SpeedReading.Application.UnitTests;

public sealed class VisualExpansionRoundRulesTests
{
    [Fact]
    public void Each_correct_training_answer_increases_distance_by_one_without_changing_duration()
    {
        VisualExpansionRoundRules.AdvanceDifficulty(
            currentDegrees: 4,
            targetDegrees: 30,
            currentDisplayDurationMs: 400,
            isCorrect: true)
            .Should().Be(new VisualExpansionDifficulty(5, 400));
        VisualExpansionRoundRules.AdvanceDifficulty(5, 30, 400, true)
            .Should().Be(new VisualExpansionDifficulty(6, 400));
    }

    [Fact]
    public void Wrong_answer_does_not_undo_progress_or_delay_the_next_correct_increase()
    {
        var first = VisualExpansionRoundRules.AdvanceDifficulty(8, 16, 400, true);
        var wrong = VisualExpansionRoundRules.AdvanceDifficulty(first.Degrees, 16, first.DisplayDurationMs, false, first.ConsecutiveCorrect);
        var next = VisualExpansionRoundRules.AdvanceDifficulty(wrong.Degrees, 16, wrong.DisplayDurationMs, true, wrong.ConsecutiveCorrect);
        first.Should().Be(new VisualExpansionDifficulty(9, 400));
        wrong.Should().Be(first);
        next.Should().Be(new VisualExpansionDifficulty(10, 400));
    }

    [Fact]
    public void Legacy_assessment_rule_keeps_its_two_unit_increase_and_duration_change()
    {
        VisualExpansionRoundRules.AdvanceDifficulty(8, 16, 400, true, staged: false)
            .Should().Be(new VisualExpansionDifficulty(10, 380));
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
            isCorrect: true,
            consecutiveCorrect: 2)
            .Should().Be(new VisualExpansionDifficulty(30, 105, 0));
    }

    [Fact]
    public void Third_correct_answer_at_target_shortens_only_duration()
    {
        VisualExpansionRoundRules.AdvanceDifficulty(30, 30, 400, true, 2)
            .Should().Be(new VisualExpansionDifficulty(30, 380, 0));
        VisualExpansionRoundRules.AdvanceDifficulty(30, 30, 100, true, 2)
            .Should().Be(new VisualExpansionDifficulty(30, 100, 0));
    }

    [Fact]
    public void Wrong_answer_at_target_resets_duration_streak_without_changing_settings()
    {
        VisualExpansionRoundRules.AdvanceDifficulty(16, 16, 1200, false, 2)
            .Should().Be(new VisualExpansionDifficulty(16, 1200, 0));
    }

    [Fact]
    public void Following_round_avoids_previous_stimuli_and_remains_deterministic()
    {
        var previous = VisualExpansionRoundRules.CreateStimuli(42, 0, "letter", 2);
        var next = VisualExpansionRoundRules.CreateStimuli(42, 1, "letter", 2, previous);
        next.Should().NotIntersectWith(previous);
        next.Should().Equal(VisualExpansionRoundRules.CreateStimuli(42, 1, "letter", 2, previous));
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
