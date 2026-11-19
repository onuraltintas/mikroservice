using CourseManagement.Domain;

namespace CourseManagement.Application.UnitTests;

/// <summary>19.11.2026: a series may run a school year, every N weeks, or once a month.</summary>
public sealed class LessonSeriesPatternTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
    private const int Monday = 1, Tuesday = 2, Wednesday = 4;

    private static LessonSeries Series(DateOnly start, DateOnly end, int mask, int every = 1, int? monthlyWeek = null, DateOnly? anchor = null) =>
        LessonSeries.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(),
            CourseManagementTime.ZoneId, start, end, new TimeOnly(15, 0), 60, mask, Now, null, every, monthlyWeek, anchor);

    private static DateOnly[] Days(LessonSeries series) => series.Expand(CourseManagementTime.Turkey)
        .Select(item => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.StartsAt, CourseManagementTime.Turkey).DateTime)).ToArray();

    [Fact]
    public void A_series_can_run_a_whole_school_year()
    {
        var series = Series(new DateOnly(2026, 9, 14), new DateOnly(2027, 6, 18), Monday | Wednesday);

        Assert.Equal(80, Days(series).Length);
        Assert.Throws<ArgumentException>(() => Series(new DateOnly(2026, 9, 14), new DateOnly(2027, 9, 16), Monday));
    }

    [Fact]
    public void Every_other_week_counts_from_the_first_week()
    {
        var days = Days(Series(new DateOnly(2026, 11, 23), new DateOnly(2026, 12, 31), Tuesday, every: 2));

        Assert.Equal([new DateOnly(2026, 11, 24), new DateOnly(2026, 12, 8), new DateOnly(2026, 12, 22)], days);
    }

    [Fact]
    public void An_edited_schedule_keeps_the_rhythm_of_the_original_start()
    {
        var days = Days(Series(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), Tuesday, every: 2, anchor: new DateOnly(2026, 11, 23)));

        Assert.Equal([new DateOnly(2026, 12, 8), new DateOnly(2026, 12, 22)], days);
    }

    [Fact]
    public void Monthly_series_fall_on_the_chosen_week_of_each_month()
    {
        var first = Days(Series(new DateOnly(2026, 12, 1), new DateOnly(2027, 3, 31), Tuesday, monthlyWeek: 1));
        var last = Days(Series(new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 28), Monday, monthlyWeek: 5));

        Assert.Equal([new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 5), new DateOnly(2027, 2, 2), new DateOnly(2027, 3, 2)], first);
        Assert.Equal([new DateOnly(2026, 12, 28), new DateOnly(2027, 1, 25), new DateOnly(2027, 2, 22)], last);
    }

    [Fact]
    public void A_monthly_series_needs_one_weekday_and_a_valid_week()
    {
        Assert.Throws<ArgumentException>(() => Series(new DateOnly(2026, 12, 1), new DateOnly(2027, 3, 31), Monday | Tuesday, monthlyWeek: 1));
        Assert.Throws<ArgumentException>(() => Series(new DateOnly(2026, 12, 1), new DateOnly(2027, 3, 31), Monday, monthlyWeek: 6));
        Assert.Throws<ArgumentOutOfRangeException>(() => Series(new DateOnly(2026, 12, 1), new DateOnly(2027, 3, 31), Monday, every: 5));
    }
}
