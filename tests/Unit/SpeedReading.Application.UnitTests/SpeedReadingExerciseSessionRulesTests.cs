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
    public void Reading_seconds_use_the_reading_window_and_reading_pauses()
    {
        var sessionStart = new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);
        var sessionEnd = new DateTime(2026, 8, 26, 10, 5, 0, DateTimeKind.Utc);
        var readingStart = new DateTime(2026, 8, 26, 10, 0, 30, DateTimeKind.Utc);
        var readingEnd = new DateTime(2026, 8, 26, 10, 2, 30, DateTimeKind.Utc);

        SpeedReadingExerciseSessionRules.CalculateReadingSeconds(
            sessionStart,
            sessionEnd,
            readingStart,
            readingEnd,
            30).Should().Be(90);
    }

    [Fact]
    public void Reading_seconds_fall_back_to_the_active_session_when_no_reading_window_exists()
    {
        var sessionStart = new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);
        var sessionEnd = new DateTime(2026, 8, 26, 10, 5, 0, DateTimeKind.Utc);

        SpeedReadingExerciseSessionRules.CalculateReadingSeconds(
            sessionStart,
            sessionEnd,
            null,
            null,
            60).Should().Be(240);
    }

    [Fact]
    public void Composite_score_is_bounded_and_keeps_comprehension_and_speed_separate()
    {
        SpeedReadingExerciseSessionRules.CalculateCompositeScore(80, 200).Should().Be(64);
        SpeedReadingExerciseSessionRules.CalculateCompositeScore(80, 2_000).Should().Be(88);
        SpeedReadingExerciseSessionRules.CalculateCompositeScore(150, 2_000).Should().Be(100);
        SpeedReadingExerciseSessionRules.CalculateCompositeScore(80, null).Should().Be(80);
        SpeedReadingExerciseSessionRules.CalculateCompositeScore(80, -10).Should().Be(80);
    }

    [Theory]
    [InlineData(300, 60, 300)]
    [InlineData(300, 2, null)]
    [InlineData(10, 60, null)]
    [InlineData(2_000, 60, null)]
    public void Validates_raw_wpm_before_exposing_it_as_a_measurement(
        int wordsRead,
        int readingSeconds,
        int? expectedWpm)
    {
        SpeedReadingExerciseSessionRules.CalculateValidatedRawWpm(wordsRead, readingSeconds)
            .Should().Be(expectedWpm.HasValue ? (decimal?)expectedWpm.Value : null);
    }

    [Theory]
    [InlineData(0, 0, 0, SpeedReadingMeasurementStatus.NotMeasured)]
    [InlineData(3, 0, 1, SpeedReadingMeasurementStatus.Measured)]
    [InlineData(0, 2, 0, SpeedReadingMeasurementStatus.Measured)]
    public void Distinguishes_completed_sessions_from_measured_outcomes(
        int questionCount,
        int correctCount,
        int incorrectCount,
        SpeedReadingMeasurementStatus expectedStatus)
    {
        SpeedReadingExerciseSessionRules.ResolveMeasurementStatus(
            questionCount,
            correctCount,
            incorrectCount).Should().Be(expectedStatus);
    }

    [Fact]
    public void Xp_is_zero_for_a_zero_score_and_has_a_floor_for_a_valid_attempt()
    {
        SpeedReadingExerciseSessionRules.CalculateXp(0, 0, 90).Should().Be(0);
        SpeedReadingExerciseSessionRules.CalculateXp(50, 50, 30).Should().BeGreaterThanOrEqualTo(10);
    }
}
