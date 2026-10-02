using Coaching.Application.StudyPlanning;

namespace Coaching.Application.CatalogAdministration;

public enum CatalogKind { Lessons, Units, Topics, Schools, UniversityPrograms }

public sealed record AdminCatalogFilter
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? Search { get; init; }
    public bool? IsActive { get; init; }
    public string? Source { get; init; }
}

public sealed record AdminCatalogRow
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Source { get; init; }
    public required string SourceId { get; init; }
    public bool IsActive { get; init; }
    public Guid? LessonId { get; init; }
    public Guid? UnitId { get; init; }
    public Guid? ParentId { get; init; }
    public int? GradeNumber { get; init; }
    public string? ExamCode { get; init; }
    public int? DisplayOrder { get; init; }
    public int? EstimatedMinutes { get; init; }
    public string? UniversityName { get; init; }
    public string? ProgramCode { get; init; }
    public string? ScoreType { get; init; }
    public decimal? MinimumScore { get; init; }
    public int? ScoreYear { get; init; }
    public string? City { get; init; }
    public string? District { get; init; }
    public string? ProvinceId { get; init; }
    public string? DistrictId { get; init; }
}

public interface ICoachingAdminCatalogReader
{
    Task<TargetSearchPage<AdminCatalogRow>> ListAsync(CatalogKind kind, AdminCatalogFilter filter, CancellationToken cancellationToken);
}
