using Coaching.Application.Queries;

namespace Identity.API.IntegrationTests;

// Roadmap D7: the teacher analysis can be run for a chosen date range (Türkiye days), at most one year.
public sealed class CoachingAnalyticsPeriodTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void WithoutDatesTheLastThirtyDaysAreUsed()
    {
        var (from, to) = CoachingAnalyticsPeriod.Resolve(null, null, Now);
        Assert.Equal((Now.AddDays(-30), Now), (from, to));
    }

    [Fact]
    public void ChosenDaysCoverWholeTurkeyDaysAndAreLimitedToAYear()
    {
        var (from, to) = CoachingAnalyticsPeriod.Resolve(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), Now);
        Assert.Equal(new DateTime(2026, 8, 31, 21, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(new DateTime(2026, 9, 30, 21, 0, 0, DateTimeKind.Utc), to);
        Assert.Throws<ArgumentException>(() => CoachingAnalyticsPeriod.Resolve(new DateOnly(2026, 10, 1), new DateOnly(2026, 9, 1), Now));
        Assert.Throws<ArgumentException>(() => CoachingAnalyticsPeriod.Resolve(new DateOnly(2025, 1, 1), new DateOnly(2026, 9, 1), Now));
        Assert.Throws<ArgumentException>(() => CoachingAnalyticsPeriod.Resolve(new DateOnly(2026, 9, 1), null, Now));
    }
}
