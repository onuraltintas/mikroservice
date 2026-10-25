namespace CourseManagement.Domain;

public enum InvitationStatus { Pending = 1, Accepted = 2, Cancelled = 3, Expired = 4 }

public sealed class WorkspaceInvitation
{
    private WorkspaceInvitation() { }
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid TargetUserId { get; private set; }
    public Guid? StudentRecordId { get; private set; }
    public WorkspaceRole Role { get; private set; }
    public InvitationStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public static WorkspaceInvitation Create(Guid id, Guid workspaceId, Guid targetUserId, WorkspaceRole role, DateTimeOffset now,
        Guid? studentRecordId = null)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || targetUserId == Guid.Empty || role is not (WorkspaceRole.Teacher or WorkspaceRole.Student)
            || (studentRecordId.HasValue && (studentRecordId.Value == Guid.Empty || role != WorkspaceRole.Student)))
            throw new ArgumentException("Invitation target and role are invalid.");
        return new WorkspaceInvitation { Id = id, WorkspaceId = workspaceId, TargetUserId = targetUserId,
            StudentRecordId = studentRecordId, Role = role, Status = InvitationStatus.Pending, ExpiresAt = now.AddDays(7) };
    }

    public bool CanAccept(Guid userId, DateTimeOffset now) => TargetUserId == userId && Status == InvitationStatus.Pending && now < ExpiresAt;
    public void Accept(Guid userId, DateTimeOffset now)
    {
        if (!CanAccept(userId, now)) throw new InvalidOperationException("Invitation is not available.");
        Status = InvitationStatus.Accepted;
    }
    public void Cancel()
    {
        if (Status != InvitationStatus.Pending) throw new InvalidOperationException("Only pending invitations can be cancelled.");
        Status = InvitationStatus.Cancelled;
    }
    public void Expire(DateTimeOffset now)
    {
        if (Status != InvitationStatus.Pending || now < ExpiresAt) throw new InvalidOperationException("Invitation has not expired.");
        Status = InvitationStatus.Expired;
    }
}
