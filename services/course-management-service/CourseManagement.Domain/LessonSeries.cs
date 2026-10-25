namespace CourseManagement.Domain;

public sealed class LessonSeries
{
    private LessonSeries() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid CourseId { get; private set; }
    public Guid? GroupId { get; private set; }
    public Guid? StudentId { get; private set; }
    public Guid? RoomId { get; private set; }
    public Guid TeacherUserId { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public int DurationMinutes { get; private set; }
    public int DaysOfWeekMask { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static LessonSeries Create(Guid id, Guid workspaceId, Guid courseId, Guid? groupId, Guid? studentId,
        Guid teacherUserId, string timeZoneId, DateOnly startsOn, DateOnly endsOn, TimeOnly startTime,
        int durationMinutes, int daysOfWeekMask, DateTimeOffset now, Guid? roomId = null)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || courseId == Guid.Empty || teacherUserId == Guid.Empty
            || (groupId is null) == (studentId is null) || groupId == Guid.Empty || studentId == Guid.Empty || roomId == Guid.Empty)
            throw new ArgumentException("Lesson series identifiers and one participant target are required.");
        ArgumentNullException.ThrowIfNull(timeZoneId);
        var normalizedTimeZoneId = timeZoneId.Trim();
        if (normalizedTimeZoneId.Length is < 1 or > 80)
            throw new ArgumentException("A valid time zone identifier is required.", nameof(timeZoneId));
        if (endsOn < startsOn || endsOn.DayNumber - startsOn.DayNumber > 180)
            throw new ArgumentException("Lesson series must end within 180 days of its start.", nameof(endsOn));
        if (durationMinutes is < 1 or > 480)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes));
        if (daysOfWeekMask is < 1 or > 127)
            throw new ArgumentOutOfRangeException(nameof(daysOfWeekMask));

        return new LessonSeries
        {
            Id = id,
            WorkspaceId = workspaceId,
            CourseId = courseId,
            GroupId = groupId,
            StudentId = studentId,
            RoomId = roomId,
            TeacherUserId = teacherUserId,
            TimeZoneId = normalizedTimeZoneId,
            StartsOn = startsOn,
            EndsOn = endsOn,
            StartTime = startTime,
            DurationMinutes = durationMinutes,
            DaysOfWeekMask = daysOfWeekMask,
            CreatedAt = now.ToUniversalTime()
        };
    }

    public IReadOnlyList<(DateTimeOffset StartsAt, DateTimeOffset EndsAt)> Expand(TimeZoneInfo timeZone)
    {
        var occurrences = new List<(DateTimeOffset StartsAt, DateTimeOffset EndsAt)>();
        for (var date = StartsOn; ; date = date.AddDays(1))
        {
            var mondayFirstDayIndex = ((int)date.DayOfWeek + 6) % 7;
            if ((DaysOfWeekMask & (1 << mondayFirstDayIndex)) != 0)
            {
                var localStart = DateTime.SpecifyKind(date.ToDateTime(StartTime), DateTimeKind.Unspecified);
                if (timeZone.IsInvalidTime(localStart) || timeZone.IsAmbiguousTime(localStart))
                    throw new InvalidOperationException("A recurring lesson falls in an ambiguous or nonexistent local time.");

                var startsAt = new DateTimeOffset(localStart, timeZone.GetUtcOffset(localStart)).ToUniversalTime();
                occurrences.Add((startsAt, startsAt.AddMinutes(DurationMinutes)));
            }
            if (date == EndsOn) break;
        }
        return occurrences;
    }

    public void UpdateSchedule(DateOnly endsOn, TimeOnly startTime, int durationMinutes, int daysOfWeekMask, Guid? roomId)
    {
        if (roomId == Guid.Empty) throw new ArgumentException("Optional room identifier cannot be empty.", nameof(roomId));
        if (endsOn < StartsOn || endsOn.DayNumber - StartsOn.DayNumber > 180)
            throw new ArgumentOutOfRangeException(nameof(endsOn));
        if (durationMinutes is < 1 or > 480) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
        if (daysOfWeekMask is < 1 or > 127) throw new ArgumentOutOfRangeException(nameof(daysOfWeekMask));

        EndsOn = endsOn;
        StartTime = startTime;
        DurationMinutes = durationMinutes;
        DaysOfWeekMask = daysOfWeekMask;
        RoomId = roomId;
    }
}
