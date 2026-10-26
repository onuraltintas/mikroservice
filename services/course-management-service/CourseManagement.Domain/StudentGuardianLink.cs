namespace CourseManagement.Domain;

public enum GuardianRelationship { Parent = 1, Mother = 2, Father = 3, LegalGuardian = 4, Other = 5 }

public sealed class StudentGuardianLink
{
    private StudentGuardianLink() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid GuardianContactId { get; private set; }
    public GuardianRelationship Relationship { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static StudentGuardianLink Create(Guid id, Guid workspaceId, Guid studentId, Guid guardianContactId,
        GuardianRelationship relationship, DateTimeOffset now)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || studentId == Guid.Empty || guardianContactId == Guid.Empty
            || !Enum.IsDefined(relationship))
            throw new ArgumentException("Guardian link details are invalid.");
        return new StudentGuardianLink
        {
            Id = id,
            WorkspaceId = workspaceId,
            StudentId = studentId,
            GuardianContactId = guardianContactId,
            Relationship = relationship,
            IsActive = true,
            CreatedAt = now
        };
    }

    public void SetRelationship(GuardianRelationship relationship)
    {
        if (!Enum.IsDefined(relationship)) throw new ArgumentException("Guardian relationship is invalid.", nameof(relationship));
        Relationship = relationship;
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;
}
