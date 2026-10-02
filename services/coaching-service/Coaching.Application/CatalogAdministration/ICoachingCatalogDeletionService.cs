namespace Coaching.Application.CatalogAdministration;

public sealed record CatalogDeleteRequest(string Fingerprint, string Reason, Guid ConfirmId);
public sealed record CatalogUsage(Guid Id, string Name, string Fingerprint,
    int CatalogReferences, int PlanReferences, int GoalReferences, int ExamReferences)
{
    public bool CanDelete => CatalogReferences == 0 && PlanReferences == 0 && GoalReferences == 0 && ExamReferences == 0;
}

public interface ICoachingCatalogDeletionService
{
    Task<CatalogUsage> GetUsageAsync(CatalogKind kind, Guid id, CancellationToken cancellationToken);
    Task DeleteAsync(CatalogKind kind, Guid id, CatalogDeleteRequest request, CancellationToken cancellationToken);
}
