namespace CourseManagement.Domain;

/// <summary>
/// E3 #34 (user decision 06.11.2026): no time zone choice anywhere. Lesson series and teacher hours are in Türkiye time
/// (Europe/Istanbul); a zone a client still sends is ignored. Rows saved earlier keep their stored zone (their lessons are
/// already stored as instants and stay where they are).
/// </summary>
public static class CourseManagementTime
{
    public const string ZoneId = "Europe/Istanbul";
    public static readonly TimeZoneInfo Turkey = TimeZoneInfo.FindSystemTimeZoneById(ZoneId);

    /// <summary>
    /// Does a lesson fall inside one of the teacher's weekly hours? The hours are Türkiye time, also those stored earlier with
    /// another zone (the panel says so); no windows means no limit.
    /// </summary>
    public static bool FitsTeacherHours(IReadOnlyList<TeacherAvailabilityWindow> windows, DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (windows.Count == 0) return true;
        var localStart = TimeZoneInfo.ConvertTime(startsAt, Turkey);
        var localEnd = TimeZoneInfo.ConvertTime(endsAt, Turkey);
        return DateOnly.FromDateTime(localStart.DateTime) == DateOnly.FromDateTime(localEnd.DateTime)
            && windows.Any(window => ((int)localStart.DayOfWeek + 6) % 7 == window.Weekday
                && TimeOnly.FromDateTime(localStart.DateTime) >= window.StartTime
                && TimeOnly.FromDateTime(localEnd.DateTime) <= window.EndTime);
    }
}
