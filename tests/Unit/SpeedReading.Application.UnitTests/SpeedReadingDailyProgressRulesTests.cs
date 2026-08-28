using FluentAssertions;
using SpeedReading.Application.DailyProgress;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingDailyProgressRulesTests
{
    [Fact]
    public void Uses_the_frontend_success_rate_when_the_legacy_score_alias_is_missing()
    {
        SpeedReadingDailyProgressRules.ResolveScore(null, 82.5m).Should().Be(82.5m);
        SpeedReadingDailyProgressRules.ResolveScore(77m, 82.5m).Should().Be(77m);
    }

    [Fact]
    public void Accepts_a_valid_completion_idempotency_key()
    {
        SpeedReadingDailyProgressRules.ValidateIdempotencyKey("daily-completion-1234")
            .Should().Be("daily-completion-1234");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too short")]
    public void Rejects_missing_or_invalid_completion_idempotency_keys(string? key)
    {
        var action = () => SpeedReadingDailyProgressRules.ValidateIdempotencyKey(key);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rejects_invalid_scores_and_missing_duration()
    {
        FluentActions.Invoking(() => SpeedReadingDailyProgressRules.ResolveScore(null, 101m))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => SpeedReadingDailyProgressRules.ResolveScore(null, null))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => SpeedReadingDailyProgressRules.ValidateDuration(0))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Does_not_increment_streak_for_multiple_completions_on_the_same_day()
    {
        var completionDate = new DateTime(2026, 8, 26, 14, 0, 0, DateTimeKind.Utc);

        SpeedReadingDailyProgressRules.CalculateNextStreak(
            new DateTime(2026, 8, 26, 9, 0, 0, DateTimeKind.Utc),
            completionDate,
            3).Should().Be(3);
    }

    [Fact]
    public void Increments_streak_only_for_the_previous_calendar_day()
    {
        var completionDate = new DateTime(2026, 8, 26, 14, 0, 0, DateTimeKind.Utc);

        SpeedReadingDailyProgressRules.CalculateNextStreak(
            new DateTime(2026, 8, 25, 23, 0, 0, DateTimeKind.Utc),
            completionDate,
            3).Should().Be(4);

        SpeedReadingDailyProgressRules.CalculateNextStreak(
            new DateTime(2026, 8, 23, 23, 0, 0, DateTimeKind.Utc),
            completionDate,
            3).Should().Be(1);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(7, 1, 7)]
    [InlineData(8, 2, 1)]
    public void Converts_cumulative_day_to_week_and_relative_day(int cumulativeDay, int expectedWeek, int expectedDay)
    {
        SpeedReadingDailyProgressRules.GetWeekAndDay(cumulativeDay)
            .Should().Be((expectedWeek, expectedDay));
    }
}
