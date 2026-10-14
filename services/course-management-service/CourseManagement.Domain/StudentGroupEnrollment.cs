namespace CourseManagement.Domain;

public enum EnrollmentStatus { Active = 1, Archived = 2 }

public sealed class StudentGroupEnrollment
{
    private StudentGroupEnrollment() { }
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid StudentId { get; private set; }
    public EnrollmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static StudentGroupEnrollment Create(Guid id, Guid workspaceId, Guid groupId, Guid studentId, DateTimeOffset now)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || groupId == Guid.Empty || studentId == Guid.Empty)
            throw new ArgumentException("Enrollment identifiers are required.");
        return new StudentGroupEnrollment { Id = id, WorkspaceId = workspaceId, GroupId = groupId,
            StudentId = studentId, Status = EnrollmentStatus.Active, CreatedAt = now };
    }

    public void Archive() => Status = EnrollmentStatus.Archived;
    public void Reactivate() => Status = EnrollmentStatus.Active;
}
