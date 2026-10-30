using EduPlatform.Shared.Kernel.Exceptions;

namespace SpeedReading.Application.Analytics;

public static class SpeedReadingReportCalendar
{
    public const string TimeZoneId = "Europe/Istanbul";
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
    public static DateTime LocalTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    public static DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(LocalTime(utc));

    /// <summary>UTC instant of 00:00 on the given Istanbul calendar day.</summary>
    public static DateTime LocalDayStartUtc(DateOnly day) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Zone);

    /// <summary>
    /// H22 (report audit 30.10.2026): a report range picked as calendar days ("2026-10-30", no time and no zone) means whole
    /// Istanbul days, so the start is local 00:00 and the end includes the whole last day. Values with a time or a zone stay
    /// exact instants. Returns an inclusive UTC range.
    /// </summary>
    public static (DateTime Start, DateTime End) ResolveRange(
        DateTime? dateFrom,
        DateTime? dateTo,
        DateTime nowUtc,
        int defaultDays,
        int maxRangeDays = 366)
    {
        var end = dateTo is { } to
            ? IsCalendarDay(to) ? LocalDayStartUtc(DateOnly.FromDateTime(to).AddDays(1)).AddTicks(-1) : ToUtc(to)
            : ToUtc(nowUtc);
        var start = dateFrom is { } from
            ? IsCalendarDay(from) ? LocalDayStartUtc(DateOnly.FromDateTime(from)) : ToUtc(from)
            : end.AddDays(-defaultDays);
        if (start > end)
            throw new BusinessRuleException("SpeedReading.Analytics.DateRange.Invalid", "Başlangıç tarihi bitiş tarihinden sonra olamaz.");
        if ((end - start).TotalDays > maxRangeDays + 1)
            throw new BusinessRuleException("SpeedReading.Analytics.DateRange.TooLarge", $"Analitik tarih aralığı en fazla {maxRangeDays} gün olabilir.");
        return (start, end);
    }

    /// <summary>The later of a requested range start and an earliest allowed instant (UTC), keeping calendar-day input when it is later.</summary>
    public static DateTime? NotBefore(DateTime? dateFrom, DateTime earliestUtc, DateTime nowUtc, int defaultDays)
    {
        var earliest = ToUtc(earliestUtc);
        var requested = dateFrom is { } from
            ? IsCalendarDay(from) ? LocalDayStartUtc(DateOnly.FromDateTime(from)) : ToUtc(from)
            : ToUtc(nowUtc).AddDays(-defaultDays);
        return requested >= earliest ? dateFrom : earliest;
    }

    private static bool IsCalendarDay(DateTime value) => value.Kind == DateTimeKind.Unspecified && value.TimeOfDay == TimeSpan.Zero;

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };
}
