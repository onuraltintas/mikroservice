namespace CourseManagement.Domain;

public sealed class TeacherAvailabilityWindow
{
    private TeacherAvailabilityWindow() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid TeacherUserId { get; private set; }
    public int Weekday { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static TeacherAvailabilityWindow Create(Guid id, Guid workspaceId, Guid teacherUserId, int weekday,
        TimeOnly startTime, TimeOnly endTime, string timeZoneId, Guid createdByUserId, DateTimeOffset now)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || teacherUserId == Guid.Empty || createdByUserId == Guid.Empty)
            throw new ArgumentException("Availability identifiers are required.");
        if (weekday is < 0 or > 6 || endTime <= startTime)
            throw new ArgumentException("Availability must have a valid weekday and a same-day time range.");
        ArgumentNullException.ThrowIfNull(timeZoneId);
        var normalizedTimeZoneId = timeZoneId.Trim();
        if (normalizedTimeZoneId.Length is < 1 or > 80)
            throw new ArgumentException("A valid time zone identifier is required.", nameof(timeZoneId));

        return new TeacherAvailabilityWindow
        {
            Id = id,
            WorkspaceId = workspaceId,
            TeacherUserId = teacherUserId,
            Weekday = weekday,
            StartTime = startTime,
            EndTime = endTime,
            TimeZoneId = normalizedTimeZoneId,
            CreatedByUserId = createdByUserId,
            CreatedAt = now.ToUniversalTime()
        };
    }
}
