using FluentAssertions;
using SpeedReading.Application.ExerciseSessions;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingExerciseSessionRulesTests
{
    [Fact]
    public void Accuracy_is_zero_when_no_actions_have_been_recorded()
    {
        SpeedReadingExerciseSessionRules.CalculateAccuracy(0, 0).Should().Be(0);
    }

    [Fact]
    public void Accuracy_is_calculated_from_correct_and_incorrect_actions()
    {
        SpeedReadingExerciseSessionRules.CalculateAccuracy(3, 1).Should().Be(75);
    }

    [Fact]
    public void Active_seconds_exclude_paused_time_and_open_pause()
    {
        var start = new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 8, 26, 10, 10, 0, DateTimeKind.Utc);
        var pausedAt = new DateTime(2026, 8, 26, 10, 8, 0, DateTimeKind.Utc);

        SpeedReadingExerciseSessionRules.CalculateActiveSeconds(
            start,
            end,
            120,
            pausedAt,
            isPaused: true).Should().Be(360);
    }

    [Fact]
    public void Xp_is_zero_for_a_zero_score_and_has_a_floor_for_a_valid_attempt()
    {
        SpeedReadingExerciseSessionRules.CalculateXp(0, 0, 90).Should().Be(0);
        SpeedReadingExerciseSessionRules.CalculateXp(50, 50, 30).Should().BeGreaterThanOrEqualTo(10);
    }
}
