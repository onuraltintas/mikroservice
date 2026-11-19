namespace CourseManagement.Domain;

public sealed class LessonSeries
{
    /// <summary>19.11.2026: a series may run a school year (it was 180 days).</summary>
    public const int MaxSpanDays = 366;

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
    /// <summary>1 = every week, 2 = every other week … up to 4. Counted in weeks from the series' first week.</summary>
    public int RepeatEveryWeeks { get; private set; } = 1;
    /// <summary>Set for a monthly series: the week of the month (1–4, or 5 for the last), on the one weekday in the mask.</summary>
    public int? MonthlyWeekOfMonth { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    // Not stored: an edited schedule is expanded from today but keeps the every-other-week rhythm of the original start.
    private DateOnly? cycleAnchor;

    public static LessonSeries Create(Guid id, Guid workspaceId, Guid courseId, Guid? groupId, Guid? studentId,
        Guid teacherUserId, string timeZoneId, DateOnly startsOn, DateOnly endsOn, TimeOnly startTime,
        int durationMinutes, int daysOfWeekMask, DateTimeOffset now, Guid? roomId = null,
        int repeatEveryWeeks = 1, int? monthlyWeekOfMonth = null, DateOnly? cycleAnchor = null)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || courseId == Guid.Empty || teacherUserId == Guid.Empty
            || (groupId is null) == (studentId is null) || groupId == Guid.Empty || studentId == Guid.Empty || roomId == Guid.Empty)
            throw new ArgumentException("Lesson series identifiers and one participant target are required.");
        ArgumentNullException.ThrowIfNull(timeZoneId);
        var normalizedTimeZoneId = timeZoneId.Trim();
        if (normalizedTimeZoneId.Length is < 1 or > 80)
            throw new ArgumentException("A valid time zone identifier is required.", nameof(timeZoneId));
        if (endsOn < startsOn || endsOn.DayNumber - startsOn.DayNumber > MaxSpanDays)
            throw new ArgumentException($"Lesson series must end within {MaxSpanDays} days of its start.", nameof(endsOn));
        if (repeatEveryWeeks is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(repeatEveryWeeks));
        if (monthlyWeekOfMonth is not null && (monthlyWeekOfMonth is < 1 or > 5 || repeatEveryWeeks != 1
                || System.Numerics.BitOperations.PopCount((uint)daysOfWeekMask) != 1))
            throw new ArgumentException("A monthly series falls on one weekday, in one week of the month.", nameof(monthlyWeekOfMonth));
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
            RepeatEveryWeeks = repeatEveryWeeks,
            MonthlyWeekOfMonth = monthlyWeekOfMonth,
            cycleAnchor = cycleAnchor,
            CreatedAt = now.ToUniversalTime()
        };
    }

    public IReadOnlyList<(DateTimeOffset StartsAt, DateTimeOffset EndsAt)> Expand(TimeZoneInfo timeZone)
    {
        var occurrences = new List<(DateTimeOffset StartsAt, DateTimeOffset EndsAt)>();
        var anchor = cycleAnchor ?? StartsOn;
        var firstMonday = anchor.AddDays(-(((int)anchor.DayOfWeek + 6) % 7));
        for (var date = StartsOn; ; date = date.AddDays(1))
        {
            var mondayFirstDayIndex = ((int)date.DayOfWeek + 6) % 7;
            if ((DaysOfWeekMask & (1 << mondayFirstDayIndex)) != 0 && Repeats(date, firstMonday))
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

    /// <summary>Whether a matching weekday is a lesson day: its week in the cycle, or its week of the month.</summary>
    private bool Repeats(DateOnly date, DateOnly firstMonday)
    {
        if (MonthlyWeekOfMonth is int week)
            return week == 5 ? date.AddDays(7).Month != date.Month : (date.Day - 1) / 7 + 1 == week;
        return (date.DayNumber - firstMonday.DayNumber) / 7 % RepeatEveryWeeks == 0;
    }

    public void UpdateSchedule(DateOnly endsOn, TimeOnly startTime, int durationMinutes, int daysOfWeekMask, Guid? roomId)
    {
        if (roomId == Guid.Empty) throw new ArgumentException("Optional room identifier cannot be empty.", nameof(roomId));
        if (endsOn < StartsOn || endsOn.DayNumber - StartsOn.DayNumber > MaxSpanDays)
            throw new ArgumentOutOfRangeException(nameof(endsOn));
        if (MonthlyWeekOfMonth is not null && System.Numerics.BitOperations.PopCount((uint)daysOfWeekMask) != 1)
            throw new ArgumentOutOfRangeException(nameof(daysOfWeekMask));
        if (durationMinutes is < 1 or > 480) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
        if (daysOfWeekMask is < 1 or > 127) throw new ArgumentOutOfRangeException(nameof(daysOfWeekMask));

        EndsOn = endsOn;
        StartTime = startTime;
        DurationMinutes = durationMinutes;
        DaysOfWeekMask = daysOfWeekMask;
        RoomId = roomId;
        TimeZoneId = CourseManagementTime.ZoneId; // E3 #34: a changed schedule is in Türkiye time
    }
}
