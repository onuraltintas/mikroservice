namespace Coaching.Application.Interfaces;

/// <summary>Verified parents (owned by Identity) who want the weekly summary of their child.</summary>
public interface ICoachingParentDirectory
{
    Task<CoachingParentSummaryRecipientPage> GetWeeklySummaryRecipientsAsync(int pageNumber, int pageSize, CancellationToken ct);
}

public sealed record CoachingParentSummaryRecipient(Guid ParentUserId, string ParentEmail, string ParentName, Guid StudentUserId, string StudentName);
public sealed record CoachingParentSummaryRecipientPage(IReadOnlyList<CoachingParentSummaryRecipient> Items, int TotalCount);
