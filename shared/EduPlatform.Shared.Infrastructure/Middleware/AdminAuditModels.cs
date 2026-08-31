namespace EduPlatform.Shared.Infrastructure.Middleware;

public sealed class AdminAuditQueryParameters
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? Search { get; init; }
    public int? StatusCode { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

public sealed record AdminAuditPage(
    IReadOnlyList<AdminAuditRecord> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminAuditFacets(
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> ResourceTypes);
