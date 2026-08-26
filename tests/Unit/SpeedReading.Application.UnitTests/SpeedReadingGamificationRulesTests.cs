using FluentAssertions;
using SpeedReading.Application.Gamification;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingGamificationRulesTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(900, 9)]
    public void Calculates_legacy_level_from_total_xp(long totalXp, int expectedLevel)
    {
        SpeedReadingGamificationRules.CalculateLevel(totalXp).Should().Be(expectedLevel);
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
}
