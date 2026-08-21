namespace Coaching.Application.Interfaces;

/// <summary>
/// Supplies the active, tenant-scoped student roster for administrative reports.
/// Identity remains the source of truth for institution and grade membership.
/// </summary>
public interface ICoachingIdentityReportClient
{
    Task<IReadOnlyCollection<Guid>> GetActiveStudentIdsAsync(
        Guid viewerUserId,
        Guid institutionId,
        int? gradeLevel,
        CancellationToken cancellationToken);
}
