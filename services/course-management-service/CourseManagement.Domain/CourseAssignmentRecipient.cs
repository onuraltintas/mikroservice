namespace CourseManagement.Domain;

public sealed class CourseAssignmentRecipient
{
    private CourseAssignmentRecipient() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid StudentId { get; private set; }

    public static CourseAssignmentRecipient Create(Guid id, Guid workspaceId, Guid assignmentId, Guid studentId)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || assignmentId == Guid.Empty || studentId == Guid.Empty)
            throw new ArgumentException("Assignment recipient identifiers are required.");
        return new CourseAssignmentRecipient { Id = id, WorkspaceId = workspaceId, AssignmentId = assignmentId, StudentId = studentId };
    }
}
