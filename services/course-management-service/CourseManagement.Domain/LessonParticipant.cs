namespace CourseManagement.Domain;

public sealed class LessonParticipant
{
    private LessonParticipant() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid LessonSessionId { get; private set; }
    public Guid StudentId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? CancelledAt { get; private set; }

    public static LessonParticipant Create(Guid id, Guid workspaceId, Guid lessonSessionId, Guid studentId)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || lessonSessionId == Guid.Empty || studentId == Guid.Empty)
            throw new ArgumentException("Lesson participant identifiers are required.");
        return new LessonParticipant
        {
            Id = id,
            WorkspaceId = workspaceId,
            LessonSessionId = lessonSessionId,
            StudentId = studentId,
            IsActive = true
        };
    }

    public void Cancel(DateTimeOffset now)
    {
        if (!IsActive) return;
        IsActive = false;
        CancelledAt = now.ToUniversalTime();
    }
}
