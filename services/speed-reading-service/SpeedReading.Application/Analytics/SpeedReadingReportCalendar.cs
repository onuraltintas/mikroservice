namespace SpeedReading.Application.Analytics;

public static class SpeedReadingReportCalendar
{
    public const string TimeZoneId = "Europe/Istanbul";
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
    public static DateTime LocalTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    public static DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(LocalTime(utc));
}
