using System.Text.Json;
using System.Text.Json.Serialization;

namespace Coaching.Application.CatalogAdministration;

public sealed record CatalogSaveRequest(string Name, string Reason, string? Fingerprint = null,
    int? GradeNumber = null, string? ExamCode = null, Guid? LessonId = null, Guid? UnitId = null,
    Guid? ParentId = null, int? DisplayOrder = null, int? EstimatedMinutes = null,
    string? UniversityName = null, string? ProgramCode = null, string? ScoreType = null,
    decimal? MinimumScore = null, int? ScoreYear = null, string? ProvinceId = null, string? DistrictId = null);
public sealed record CatalogStatusRequest(string Fingerprint, [property: JsonRequired] bool IsActive, string Reason);
public sealed record CatalogEditDocument(string Fingerprint, JsonElement Data);

public interface ICoachingCatalogManagementService
{
    Task<CatalogEditDocument> GetAsync(CatalogKind kind, Guid id, CancellationToken cancellationToken);
    Task<CatalogEditDocument> CreateAsync(CatalogKind kind, CatalogSaveRequest request, CancellationToken cancellationToken);
    Task<CatalogEditDocument> UpdateAsync(CatalogKind kind, Guid id, CatalogSaveRequest request, CancellationToken cancellationToken);
    Task<CatalogEditDocument> SetActiveAsync(CatalogKind kind, Guid id, CatalogStatusRequest request, CancellationToken cancellationToken);
}
