namespace Coaching.Application.StudyPlanning;

public sealed record TargetSearchPage<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize);
public sealed record SchoolTargetView(Guid Id, string Name, string City, string District, decimal? MinimumScore, int? ScoreYear);
public sealed record UniversityTargetView(Guid Id, string UniversityName, string Name, string? ProgramCode, string? ScoreType, decimal? MinimumScore, int? ScoreYear);

public interface ITargetSearchService
{
    Task<TargetSearchPage<SchoolTargetView>> SearchSchoolsAsync(string? search, string? city, string? district,
        int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<TargetSearchPage<UniversityTargetView>> SearchProgramsAsync(string? search, string? scoreType,
        int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
