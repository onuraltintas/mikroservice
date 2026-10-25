namespace Coaching.Application.StudyPlanning;

public sealed record TargetSearchPage<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize);
