using FluentAssertions;
using SpeedReading.Application.DailyProgress;

namespace SpeedReading.Application.UnitTests;

/// <summary>Live test 19.11.2026 (A4, decision K1): the next program day opens the next calendar day.</summary>
public sealed class OneProgramDayPerCalendarDayTests
{
    // 19.11.2026 12:00 in Istanbul (UTC+3).
    private static readonly DateTime Now = new(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Locks_the_next_day_when_the_previous_day_was_finished_today()
    {
        var finishedThisMorning = new DateTime(2026, 11, 19, 6, 30, 0, DateTimeKind.Utc);

        SpeedReadingDailyProgressRules.ApplyOneDayPerCalendarDay(2, 2, 2, finishedThisMorning, Now, false)
            .Should().Be(1);
    }

    [Fact]
    public void Opens_the_next_day_when_the_previous_day_was_finished_yesterday()
    {
        var finishedYesterday = new DateTime(2026, 11, 18, 18, 0, 0, DateTimeKind.Utc);

        SpeedReadingDailyProgressRules.ApplyOneDayPerCalendarDay(2, 2, 2, finishedYesterday, Now, false)
            .Should().Be(2);
    }

    [Fact]
    public void Uses_the_istanbul_day_not_the_utc_day()
    {
        // 18.11 22:30 UTC is 19.11 01:30 in Istanbul: the same local day as Now.
        var finishedAfterLocalMidnight = new DateTime(2026, 11, 18, 22, 30, 0, DateTimeKind.Utc);

        SpeedReadingDailyProgressRules.ApplyOneDayPerCalendarDay(3, 3, 3, finishedAfterLocalMidnight, Now, false)
            .Should().Be(2);
    }

    [Fact]
    public void Keeps_an_administrator_early_advance_grant()
    {
        var finishedThisMorning = new DateTime(2026, 11, 19, 6, 30, 0, DateTimeKind.Utc);

        // The calendar alone allows day 1; the grant opened day 2.
        SpeedReadingDailyProgressRules.ApplyOneDayPerCalendarDay(2, 2, 1, finishedThisMorning, Now, false)
            .Should().Be(2);
    }

    [Fact]
    public void Does_not_lock_staff_training_or_the_first_day()
    {
        var finishedThisMorning = new DateTime(2026, 11, 19, 6, 30, 0, DateTimeKind.Utc);

        SpeedReadingDailyProgressRules.ApplyOneDayPerCalendarDay(int.MaxValue, 5, 1, finishedThisMorning, Now, true)
            .Should().Be(int.MaxValue);
        SpeedReadingDailyProgressRules.ApplyOneDayPerCalendarDay(1, 1, 1, null, Now, false)
            .Should().Be(1);
    }
}
