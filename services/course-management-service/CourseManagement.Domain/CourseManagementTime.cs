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
}
