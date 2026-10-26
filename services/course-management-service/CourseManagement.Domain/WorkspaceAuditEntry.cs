namespace CourseManagement.Domain;

public sealed class WorkspaceAuditEntry
{
    private WorkspaceAuditEntry() { }
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid TargetId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }

    public static WorkspaceAuditEntry Create(Guid workspaceId, Guid actorUserId, Guid targetId, string action) => new()
    {
        Id = Guid.NewGuid(), WorkspaceId = workspaceId, ActorUserId = actorUserId,
        TargetId = targetId, Action = action, OccurredAt = DateTimeOffset.UtcNow
    };
}
