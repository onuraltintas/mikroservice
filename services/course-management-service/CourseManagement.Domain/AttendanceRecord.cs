namespace CourseManagement.Domain;

public enum LessonAttendanceStatus
{
    Present = 1,
    Absent = 2,
    Excused = 3,
    Late = 4
}

public sealed class AttendanceRecord
{
    private AttendanceRecord() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid LessonSessionId { get; private set; }
    public Guid StudentId { get; private set; }
    public LessonAttendanceStatus Status { get; private set; }
    public Guid MarkedByUserId { get; private set; }
    public DateTimeOffset MarkedAt { get; private set; }

    public static AttendanceRecord Create(Guid id, Guid workspaceId, Guid lessonSessionId, Guid studentId,
        LessonAttendanceStatus status, Guid markedByUserId, DateTimeOffset markedAt)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || lessonSessionId == Guid.Empty
            || studentId == Guid.Empty || markedByUserId == Guid.Empty || !Enum.IsDefined(status))
            throw new ArgumentException("Attendance identifiers and status are required.");
        return new AttendanceRecord
        {
            Id = id,
            WorkspaceId = workspaceId,
            LessonSessionId = lessonSessionId,
            StudentId = studentId,
            Status = status,
            MarkedByUserId = markedByUserId,
            MarkedAt = markedAt.ToUniversalTime()
        };
    }

    public void UpdateStatus(LessonAttendanceStatus status, Guid markedByUserId, DateTimeOffset markedAt)
    {
        if (!Enum.IsDefined(status) || markedByUserId == Guid.Empty)
            throw new ArgumentException("Attendance status and actor are required.");
        Status = status;
        MarkedByUserId = markedByUserId;
        MarkedAt = markedAt.ToUniversalTime();
    }
}
