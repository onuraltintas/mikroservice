using FluentAssertions;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Decision 12.11.2026: only active time counts as study time. A gap of more than five minutes between two
/// server-observed activity points of an open session counts as five minutes; the rest is treated as a pause.
/// </summary>
public sealed class SpeedReadingActiveTimeRulesTests
{
    private static readonly DateTime Start = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_cap_is_five_minutes()
    {
        SpeedReadingActiveTimeRules.MaxActiveGapSeconds.Should().Be(300);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(299, 0)]
    [InlineData(300, 0)]
    [InlineData(301, 1_000)]
    [InlineData(1_200, 900_000)]
    [InlineData(432_000, 431_700_000)] // five days
    public void Only_the_part_of_a_gap_beyond_the_cap_is_idle(int gapSeconds, long expectedIdleMs)
    {
        SpeedReadingActiveTimeRules
            .CalculateIdleExcessMilliseconds(Start, Start.AddSeconds(gapSeconds), SpeedReadingActiveTimeRules.MaxActiveGapSeconds)
            .Should().Be(expectedIdleMs);
    }

    [Fact]
    public void A_clock_that_went_backwards_is_not_idle_time()
    {
        SpeedReadingActiveTimeRules.CalculateIdleExcessMilliseconds(Start, Start.AddHours(-2), 300).Should().Be(0);
    }

    [Fact]
    public void Several_gaps_are_capped_one_by_one()
    {
        var points = new[] { 0, 20, 1_020, 1_050, 1_450, 1_460 }.Select(second => Start.AddSeconds(second)).ToArray();
        var idleMs = 0L;
        for (var index = 1; index < points.Length; index++)
            idleMs += SpeedReadingActiveTimeRules.CalculateIdleExcessMilliseconds(points[index - 1], points[index], 300);

        // 1000 s and 400 s gaps each count as 300 s: 1460 - 700 - 100 = 660 active seconds.
        ((points[^1] - points[0]).TotalSeconds - idleMs / 1000d).Should().Be(660);
    }

    [Theory]
    [InlineData(false, 0, 0, 0, 0, 300)]            // ordinary step
    [InlineData(true, 100, 3_000, 0, 0, 300)]       // short text: the general cap
    [InlineData(true, 400, 3_000, 0, 0, 600)]       // 400 words at 40 words per minute
    [InlineData(true, 100, 0, 1_200_000, 0, 1_200)] // the configured reading limit is a legitimate single step
    [InlineData(true, 800, 420_000, 0, 0, 1_200)]   // word count wins over a shorter paced minimum
    [InlineData(true, 100, 420_000, 0, 0, 420)]     // a paced display that itself runs for seven minutes
    [InlineData(false, 400, 420_000, 1_200_000, 0, 300)] // reading limits apply only while the text is open
    [InlineData(false, 0, 0, 0, 480, 480)]          // an eye-tracking run longer than the cap
    public void The_cap_never_cuts_a_legitimate_single_step(
        bool readingOpen, int wordCount, int readingMinimumMs, int readingMaximumMs, int trackingSeconds, int expected)
    {
        SpeedReadingActiveTimeRules
            .ResolveGapCapSeconds(readingOpen, wordCount, readingMinimumMs, readingMaximumMs, trackingSeconds)
            .Should().Be(expected);
    }

    [Fact]
    public void An_abandoned_active_session_ends_one_cap_after_its_last_activity()
    {
        var last = Start.AddMinutes(12);
        SpeedReadingActiveTimeRules.ResolveAbandonTime(last, pausedAt: null, now: Start.AddDays(2), capSeconds: 300)
            .Should().Be(last.AddSeconds(300));
    }

    [Fact]
    public void An_abandoned_session_never_ends_in_the_future()
    {
        var last = Start.AddMinutes(12);
        SpeedReadingActiveTimeRules.ResolveAbandonTime(last, null, last.AddSeconds(40), 300).Should().Be(last.AddSeconds(40));
    }

    [Fact]
    public void An_abandoned_paused_session_ends_when_it_was_paused()
    {
        var pausedAt = Start.AddMinutes(7);
        SpeedReadingActiveTimeRules.ResolveAbandonTime(Start.AddMinutes(7), pausedAt, Start.AddDays(2), 300)
            .Should().Be(pausedAt);
    }

    /// <summary>Capped idle time still counts toward a session's time limit: the timeout rule is unchanged.</summary>
    [Fact]
    public void The_time_limit_still_counts_idle_time()
    {
        var session = ExerciseSession.Start(Guid.NewGuid(), Guid.NewGuid(), null, 5, Start, timeLimitSeconds: 600);

        session.IsTimedOut(Start.AddSeconds(600)).Should().BeFalse();
        session.IsTimedOut(Start.AddSeconds(601)).Should().BeTrue();
        session.IsTimedOut(Start.AddHours(5)).Should().BeTrue();

        session.Pause(Start.AddSeconds(100));
        session.IsTimedOut(Start.AddHours(5)).Should().BeFalse();
        session.Resume(Start.AddSeconds(1_100));
        session.TotalPausedSeconds.Should().Be(1_000);
        session.IsTimedOut(Start.AddSeconds(1_600)).Should().BeFalse();
        session.IsTimedOut(Start.AddSeconds(1_601)).Should().BeTrue();
    }
}
