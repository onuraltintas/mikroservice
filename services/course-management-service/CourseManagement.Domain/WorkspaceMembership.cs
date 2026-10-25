namespace CourseManagement.Domain;

public enum WorkspaceRole { Student = 1, Teacher = 2, Owner = 3 }

public sealed class WorkspaceMembership
{
    private WorkspaceMembership() { }
    public Guid WorkspaceId { get; private set; }
    public Guid UserId { get; private set; }
    public WorkspaceRole Role { get; private set; }
    public bool IsActive { get; private set; }

    public static WorkspaceMembership Create(Guid workspaceId, Guid userId, WorkspaceRole role)
    {
        if (workspaceId == Guid.Empty || userId == Guid.Empty || !Enum.IsDefined(role))
            throw new ArgumentException("Valid workspace, user and role are required.");
        return new WorkspaceMembership { WorkspaceId = workspaceId, UserId = userId, Role = role, IsActive = true };
    }

    public static WorkspaceMembership CreateAnonymized(Guid workspaceId, Guid userId, WorkspaceRole role, bool isActive)
    {
        if (workspaceId == Guid.Empty || userId == Guid.Empty || !Enum.IsDefined(role))
            throw new ArgumentException("Valid workspace, user and role are required.");
        return new WorkspaceMembership { WorkspaceId = workspaceId, UserId = userId, Role = role, IsActive = isActive };
    }

    public void Revoke()
    {
        if (Role == WorkspaceRole.Owner) throw new InvalidOperationException("Workspace ownership cannot be revoked.");
        IsActive = false;
    }

    public void Activate(WorkspaceRole role)
    {
        if (!Enum.IsDefined(role) || role == WorkspaceRole.Owner || Role == WorkspaceRole.Owner)
            throw new InvalidOperationException("Ownership changes are not supported.");
        Role = role;
        IsActive = true;
    }
}
