namespace CourseManagement.Domain;

public enum WorkspaceRetentionStep
{
    Nothing = 0,
    /// <summary>E-mail the owner that the panel data will be deleted.</summary>
    Warn = 1,
    /// <summary>Delete the panel data; the workspace and its owner stay.</summary>
    Purge = 2
}

/// <summary>
/// maxkampüs data retention (user decisions 20.11.2026). When a plan ends and is not renewed the panel is read-only; a
/// year after the end its data is deleted, and the owner is warned 30 days before. A workspace whose owner never had a
/// plan is deleted 90 days after it was opened (its panel never opened, so there is nothing in it but the name).
/// Nothing is deleted while the product asks for no subscription, while a plan is running, or before a warning about
/// the same plan end has been out for the full 30 days (a warning sent late moves the deletion, never shortens it).
/// </summary>
public static class WorkspaceRetention
{
    public static readonly TimeSpan KeepAfterPlanEnd = TimeSpan.FromDays(365);
    public static readonly TimeSpan WarnBefore = TimeSpan.FromDays(30);
    public static readonly TimeSpan KeepWithoutPlan = TimeSpan.FromDays(90);

    public static WorkspaceRetentionStep Decide(
        bool requireSubscription,
        bool hasRunningPlan,
        DateTimeOffset? lastPlanEndsAt,
        DateTimeOffset workspaceCreatedAt,
        DateTimeOffset? warningForEndsAt,
        DateTimeOffset? warnedAt,
        DateTimeOffset? contentPurgedAt,
        DateTimeOffset now)
    {
        if (!requireSubscription || hasRunningPlan) return WorkspaceRetentionStep.Nothing;
        if (lastPlanEndsAt is not { } ended)
            return contentPurgedAt is null && now >= workspaceCreatedAt + KeepWithoutPlan ? WorkspaceRetentionStep.Purge : WorkspaceRetentionStep.Nothing;
        // Already deleted for this end; a later plan has a later end.
        if (contentPurgedAt is { } purged && purged >= ended) return WorkspaceRetentionStep.Nothing;
        var deleteAt = ended + KeepAfterPlanEnd;
        if (now < deleteAt - WarnBefore) return WorkspaceRetentionStep.Nothing;
        var warnedForThisEnd = warningForEndsAt is { } warnedEnd && warnedEnd == ended && warnedAt is not null;
        if (!warnedForThisEnd) return WorkspaceRetentionStep.Warn;
        return now >= deleteAt && now >= warnedAt!.Value + WarnBefore ? WorkspaceRetentionStep.Purge : WorkspaceRetentionStep.Nothing;
    }

    /// <summary>The day the data of a plan that ended at <paramref name="planEndsAt"/> is deleted, after a warning sent at <paramref name="warnedAt"/>.</summary>
    public static DateTimeOffset DeletionDate(DateTimeOffset planEndsAt, DateTimeOffset warnedAt)
    {
        var byEnd = planEndsAt + KeepAfterPlanEnd;
        var byWarning = warnedAt + WarnBefore;
        return byEnd > byWarning ? byEnd : byWarning;
    }
}
