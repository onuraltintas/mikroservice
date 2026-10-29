namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// Review H1: dates from the query string bind with DateTimeKind.Unspecified, which Npgsql refuses for timestamptz.
/// Like the analytics ranges, an unspecified value is read as UTC; a local value is converted.
/// </summary>
internal static class SpeedReadingQueryDates
{
    public static DateTime? Utc(DateTime? value) => value?.Kind switch
    {
        null => null,
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
        _ => value.Value.ToUniversalTime()
    };
}
