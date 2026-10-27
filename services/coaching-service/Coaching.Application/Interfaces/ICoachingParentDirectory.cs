namespace Coaching.Application.Interfaces;

/// <summary>Verified parents (owned by Identity) of Coaching students.</summary>
public interface ICoachingParentDirectory
{
    /// <summary>Verified parents who want the weekly summary of their child (roadmap Y7).</summary>
    Task<CoachingParentSummaryRecipientPage> GetWeeklySummaryRecipientsAsync(int pageNumber, int pageSize, CancellationToken ct);

    /// <summary>Roadmap O3: verified links of the given children, and of the given parent.</summary>
    Task<IReadOnlyList<CoachingParentLink>> GetParentLinksAsync(IReadOnlyCollection<Guid> studentUserIds, Guid? parentUserId, CancellationToken ct);
}

public sealed record CoachingParentLink(Guid ParentUserId, string ParentName, Guid StudentUserId, string StudentName, string Relationship);
public sealed record CoachingParentLinksRequest(IReadOnlyList<Guid> StudentUserIds, Guid? ParentUserId);

public sealed record CoachingParentSummaryRecipient(Guid ParentUserId, string ParentEmail, string ParentName, Guid StudentUserId, string StudentName);
public sealed record CoachingParentSummaryRecipientPage(IReadOnlyList<CoachingParentSummaryRecipient> Items, int TotalCount);
