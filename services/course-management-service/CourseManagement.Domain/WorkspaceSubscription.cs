namespace CourseManagement.Domain;

public sealed class WorkspaceSubscription
{
    private WorkspaceSubscription() { }

    public Guid WorkspaceId { get; private set; }
    public string PlanName { get; private set; } = string.Empty;
    public int ActiveStudentLimit { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public WorkspaceAccessStatus AccessStatus { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static WorkspaceSubscription Create(Guid workspaceId, string planName, int activeStudentLimit,
        DateTimeOffset startsAt, DateTimeOffset endsAt, WorkspaceAccessStatus accessStatus)
    {
        Validate(workspaceId, planName, activeStudentLimit, startsAt, endsAt, accessStatus);
        var now = DateTimeOffset.UtcNow;
        return new WorkspaceSubscription
        {
            WorkspaceId = workspaceId,
            PlanName = planName.Trim(),
            ActiveStudentLimit = activeStudentLimit,
            StartsAt = startsAt.ToUniversalTime(),
            EndsAt = endsAt.ToUniversalTime(),
            AccessStatus = accessStatus,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(string planName, int activeStudentLimit, DateTimeOffset startsAt,
        DateTimeOffset endsAt, WorkspaceAccessStatus accessStatus)
    {
        Validate(WorkspaceId, planName, activeStudentLimit, startsAt, endsAt, accessStatus);
        PlanName = planName.Trim();
        ActiveStudentLimit = activeStudentLimit;
        StartsAt = startsAt.ToUniversalTime();
        EndsAt = endsAt.ToUniversalTime();
        AccessStatus = accessStatus;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool CanWriteAt(DateTimeOffset now)
    {
        var instant = now.ToUniversalTime();
        return AccessStatus == WorkspaceAccessStatus.Active && instant >= StartsAt && instant < EndsAt;
    }

    private static void Validate(Guid workspaceId, string planName, int activeStudentLimit,
        DateTimeOffset startsAt, DateTimeOffset endsAt, WorkspaceAccessStatus accessStatus)
    {
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("Workspace identifier is required.", nameof(workspaceId));
        ArgumentNullException.ThrowIfNull(planName);
        var normalizedPlanName = planName.Trim();
        if (normalizedPlanName.Length is < 1 or > 80)
            throw new ArgumentException("Plan name must contain between 1 and 80 characters.", nameof(planName));
        if (activeStudentLimit < 1)
            throw new ArgumentOutOfRangeException(nameof(activeStudentLimit), "Active student limit must be positive.");
        if (endsAt.ToUniversalTime() <= startsAt.ToUniversalTime())
            throw new ArgumentException("Subscription end must be after its start.", nameof(endsAt));
        if (!Enum.IsDefined(accessStatus))
            throw new ArgumentException("Subscription access status is invalid.", nameof(accessStatus));
    }
}
