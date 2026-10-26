namespace Coaching.Domain;

/// <summary>
/// Coaching stores instants as UTC (PostgreSQL timestamptz). Values arriving without an offset are wall-clock
/// times in Türkiye, the product's time zone; values with an offset are converted. A date-only end of a range
/// covers that whole day.
/// </summary>
public static class CoachingTime
{
    public static readonly TimeZoneInfo Turkey = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => TimeZoneInfo.ConvertTimeToUtc(value, Turkey)
    };

    /// <summary>Start of a requested range (inclusive).</summary>
    public static DateTime? RangeStart(DateTime? value) => value is { } start ? ToUtc(start) : null;

    /// <summary>End of a requested range (inclusive). A date without a time means the end of that day.</summary>
    public static DateTime? RangeEnd(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Unspecified } end when end.TimeOfDay == TimeSpan.Zero
            => TimeZoneInfo.ConvertTimeToUtc(end.Date.AddDays(1), Turkey).AddTicks(-1),
        { } end => ToUtc(end)
    };
}
