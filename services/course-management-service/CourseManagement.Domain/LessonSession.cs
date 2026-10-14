namespace CourseManagement.Domain;

public enum LessonSessionStatus
{
    Scheduled = 1,
    Cancelled = 2,
    Completed = 3
}

public sealed class LessonSession
{
    private LessonSession() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid CourseId { get; private set; }
    public Guid? SeriesId { get; private set; }
    public Guid? GroupId { get; private set; }
    public Guid? RoomId { get; private set; }
    public Guid TeacherUserId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public LessonSessionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static LessonSession Create(Guid id, Guid workspaceId, Guid courseId, Guid? groupId, Guid teacherUserId,
        DateTimeOffset startsAt, DateTimeOffset endsAt, DateTimeOffset now, Guid? seriesId = null, Guid? roomId = null)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || courseId == Guid.Empty || teacherUserId == Guid.Empty)
            throw new ArgumentException("Lesson identifiers and teacher are required.");
        if (groupId == Guid.Empty || seriesId == Guid.Empty || roomId == Guid.Empty)
            throw new ArgumentException("Optional lesson identifiers cannot be empty.");
        startsAt = startsAt.ToUniversalTime();
        endsAt = endsAt.ToUniversalTime();
        if (endsAt <= startsAt) throw new ArgumentException("Lesson end must be after its start.", nameof(endsAt));

        return new LessonSession
        {
            Id = id,
            WorkspaceId = workspaceId,
            CourseId = courseId,
            SeriesId = seriesId,
            GroupId = groupId,
            RoomId = roomId,
            TeacherUserId = teacherUserId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = LessonSessionStatus.Scheduled,
            CreatedAt = now.ToUniversalTime()
        };
    }

    public void Cancel()
    {
        if (Status != LessonSessionStatus.Scheduled)
            throw new InvalidOperationException("Only scheduled lessons can be cancelled.");
        Status = LessonSessionStatus.Cancelled;
    }

    public void Complete()
    {
        if (Status != LessonSessionStatus.Scheduled)
            throw new InvalidOperationException("Only scheduled lessons can be completed.");
        Status = LessonSessionStatus.Completed;
    }

    public void Reschedule(DateTimeOffset startsAt, DateTimeOffset endsAt, Guid? roomId)
    {
        if (Status != LessonSessionStatus.Scheduled)
            throw new InvalidOperationException("Only scheduled lessons can be rescheduled.");
        if (roomId == Guid.Empty)
            throw new ArgumentException("Optional room identifier cannot be empty.", nameof(roomId));
        startsAt = startsAt.ToUniversalTime();
        endsAt = endsAt.ToUniversalTime();
        if (endsAt <= startsAt) throw new ArgumentException("Lesson end must be after its start.", nameof(endsAt));

        StartsAt = startsAt;
        EndsAt = endsAt;
        RoomId = roomId;
    }
}
