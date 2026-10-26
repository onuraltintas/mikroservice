namespace Notification.Application.Configuration;

/// <summary>
/// Formats event timestamps for notification texts in Turkey's local time.
/// Coaching and Course Management store and publish UTC, so a DateTime without a
/// kind is treated as UTC rather than as the container's (UTC) local time.
/// </summary>
public static class TurkeyTime
{
    public const string DateTimeFormat = "dd.MM.yyyy HH:mm";

    private static readonly TimeZoneInfo Zone = ResolveZone();

    public static DateTimeOffset ToLocal(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, Zone);

    public static DateTimeOffset ToLocal(DateTime value) => ToLocal(value.Kind switch
    {
        DateTimeKind.Local => new DateTimeOffset(value.ToUniversalTime(), TimeSpan.Zero),
        _ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero),
    });

    public static string Format(DateTimeOffset value) => ToLocal(value).ToString(DateTimeFormat);

    public static string Format(DateTime value) => ToLocal(value).ToString(DateTimeFormat);

    private static TimeZoneInfo ResolveZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
        catch (TimeZoneNotFoundException)
        {
            // Images without tzdata: Turkey has used a fixed UTC+3 offset since 2016.
            return TimeZoneInfo.CreateCustomTimeZone("Europe/Istanbul", TimeSpan.FromHours(3), "Türkiye", "Türkiye");
        }
    }
}
