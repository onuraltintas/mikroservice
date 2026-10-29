using FluentAssertions;
using SpeedReading.Application.Gamification;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingGamificationRulesTests
{
    // Review H5 (finding 14, decision 29.10.2026): every level is 100 XP, so the bar shows real progress.
    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(99, 1, 99)]
    [InlineData(100, 2, 0)]
    [InlineData(250, 3, 50)]
    [InlineData(900, 10, 0)]
    public void Every_level_is_one_hundred_xp(long totalXp, int expectedLevel, int expectedLevelXp)
    {
        SpeedReadingGamificationRules.CalculateLevel(totalXp).Should().Be(expectedLevel);
        SpeedReadingGamificationRules.GetCurrentLevelXp(totalXp, expectedLevel).Should().Be(expectedLevelXp);
    }

    [Fact]
    public void Does_not_increment_streak_for_multiple_activities_on_the_same_day()
    {
        var day = new DateTime(2026, 8, 26, 9, 0, 0, DateTimeKind.Utc);

        SpeedReadingGamificationRules.CalculateNextStreak(day, day.AddHours(4), 3)
            .Should().Be(3);
    }

    [Fact]
    public void Increments_streak_only_for_the_next_calendar_day()
    {
        var day = new DateTime(2026, 8, 26, 9, 0, 0, DateTimeKind.Utc);

        SpeedReadingGamificationRules.CalculateNextStreak(day, day.AddDays(1), 3)
            .Should().Be(4);
        SpeedReadingGamificationRules.CalculateNextStreak(day, day.AddDays(2), 3)
            .Should().Be(1);
    }

    [Fact]
    public void A_streak_follows_the_istanbul_calendar_day()
    {
        // Review H5 (finding 17): 23:30 and 00:30 in Istanbul are two different days even though both are the
        // same UTC date.
        var lateEvening = new DateTime(2026, 10, 28, 20, 30, 0, DateTimeKind.Utc);
        var afterMidnight = new DateTime(2026, 10, 28, 21, 30, 0, DateTimeKind.Utc);

        SpeedReadingGamificationRules.CalculateNextStreak(lateEvening, afterMidnight, 3).Should().Be(4);
        SpeedReading.Domain.Gamification.GamificationRules.IstanbulDate(afterMidnight).Should().Be(new DateTime(2026, 10, 29));
    }

    [Fact]
    public void A_streak_shows_zero_once_a_day_is_missed()
    {
        var now = new DateTime(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc);

        SpeedReading.Domain.Gamification.GamificationRules.CurrentStreak(now.AddDays(-1), 5, now).Should().Be(5);
        SpeedReading.Domain.Gamification.GamificationRules.CurrentStreak(now.AddDays(-3), 5, now).Should().Be(0);
        SpeedReading.Domain.Gamification.GamificationRules.CurrentStreak(null, 5, now).Should().Be(0);
    }

    [Theory]
    [InlineData(false, 0, false, true)]
    [InlineData(true, 69.9, false, false)]
    [InlineData(true, 70, false, true)]
    [InlineData(true, 10, true, true)]
    public void One_rule_decides_passed(bool isMeasured, double score, bool isFreeReading, bool expected)
    {
        // Review H5 (finding 16): an unmeasured completion is done, not failed; measured work passes at 70.
        SpeedReading.Application.DailyProgress.SpeedReadingDailyProgressRules.IsPassed(isMeasured, (decimal)score, isFreeReading)
            .Should().Be(expected);
    }
}
