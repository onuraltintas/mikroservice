namespace CourseManagement.Domain;

public sealed class TeacherTimeOff
{
    private TeacherTimeOff() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid TeacherUserId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public string? Reason { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static TeacherTimeOff Create(Guid id, Guid workspaceId, Guid teacherUserId, DateTimeOffset startsAt,
        DateTimeOffset endsAt, string? reason, Guid createdByUserId, DateTimeOffset now)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || teacherUserId == Guid.Empty || createdByUserId == Guid.Empty)
            throw new ArgumentException("Time-off identifiers are required.");
        startsAt = startsAt.ToUniversalTime();
        endsAt = endsAt.ToUniversalTime();
        now = now.ToUniversalTime();
        if (startsAt <= now || endsAt <= startsAt || endsAt - startsAt > TimeSpan.FromDays(90))
            throw new ArgumentException("Time-off must be in the future and cannot exceed 90 days.");
        if (reason?.Trim().Length > 300)
            throw new ArgumentException("Time-off reason cannot exceed 300 characters.", nameof(reason));

        return new TeacherTimeOff
        {
            Id = id,
            WorkspaceId = workspaceId,
            TeacherUserId = teacherUserId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CreatedByUserId = createdByUserId,
            CreatedAt = now
        };
    }
}
