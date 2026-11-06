using CourseManagement.Domain;

namespace CourseManagement.Application.UnitTests;

// E3 #34 (user decision 06.11.2026): no time zone choice; lesson series and teacher hours are in Türkiye time.
public sealed class CourseManagementTimeTests
{
    [Fact]
    public void TheOnlyZoneIsIstanbul() => Assert.Equal("Europe/Istanbul", CourseManagementTime.ZoneId);

    [Fact]
    public void AChangedScheduleOfAnOlderSeriesMovesToTurkeyTime()
    {
        var series = LessonSeries.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(),
            "Europe/London", new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 30), new TimeOnly(18, 0), 60, 127, DateTimeOffset.UtcNow);
        Assert.Equal("Europe/London", series.TimeZoneId);   // a series saved before E3 keeps its zone until it changes
        series.UpdateSchedule(new DateOnly(2026, 11, 30), new TimeOnly(18, 0), 60, 127, null);
        Assert.Equal("Europe/Istanbul", series.TimeZoneId);
        var first = series.Expand(CourseManagementTime.Turkey).First();
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 18, 0, 0, TimeSpan.FromHours(3)), first.StartsAt);
    }
}
