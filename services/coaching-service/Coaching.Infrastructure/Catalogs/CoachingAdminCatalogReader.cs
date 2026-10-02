using Coaching.Application.Authorization;
using Coaching.Application.CatalogAdministration;
using Coaching.Application.StudyPlanning;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Catalogs;

public sealed class CoachingAdminCatalogReader(CoachingDbContext db, ICoachingAdminScopeAuthorization scope)
    : ICoachingAdminCatalogReader
{
    public async Task<TargetSearchPage<AdminCatalogRow>> ListAsync(CatalogKind kind, AdminCatalogFilter filter,
        CancellationToken cancellationToken)
    {
        if (!(await scope.RequireReadScopeAsync(cancellationToken)).IsGlobal)
            throw new BusinessRuleException("Authorization.Forbidden", "Ortak katalog yönetimi yalnız global yöneticiye açıktır.");
        if (!Enum.IsDefined(kind) || filter.PageNumber is < 1 or > 10000 || filter.PageSize is < 1 or > 100
            || filter.Search?.Length > 200 || filter.Source?.Length > 100)
            throw new ArgumentException("Katalog arama ve sayfalama değerlerini kontrol edin.");
        ValidateFilters(kind, filter);
        IQueryable<AdminCatalogRow> query = kind switch
        {
            CatalogKind.Lessons => db.StudyCatalogLessons.AsNoTracking().Select(x => new AdminCatalogRow
            { Id = x.Id, Name = x.Name, Source = x.Source, SourceId = x.SourceId, IsActive = x.IsActive, GradeNumber = x.GradeNumber, ExamCode = x.ExamCode }),
            CatalogKind.Units => db.StudyCatalogUnits.AsNoTracking().Select(x => new AdminCatalogRow
            { Id = x.Id, Name = x.Name, Source = x.Source, SourceId = x.SourceId, IsActive = x.IsActive, LessonId = x.LessonId, DisplayOrder = x.DisplayOrder }),
            CatalogKind.Topics => db.StudyCatalogTopics.AsNoTracking().Select(x => new AdminCatalogRow
            { Id = x.Id, Name = x.Name, Source = x.Source, SourceId = x.SourceId, IsActive = x.IsActive,
                LessonId = x.LessonId, UnitId = x.UnitId, ParentId = x.ParentId, DisplayOrder = x.DisplayOrder, EstimatedMinutes = x.EstimatedMinutes }),
            CatalogKind.Schools => db.TargetSchools.AsNoTracking().Select(x => new AdminCatalogRow
            { Id = x.Id, Name = x.Name, Source = x.Source, SourceId = x.SourceId, IsActive = x.IsActive, City = x.City,
                District = x.District, ProvinceId = x.ProvinceId, DistrictId = x.DistrictId, MinimumScore = x.MinimumScore, ScoreYear = x.ScoreYear }),
            CatalogKind.UniversityPrograms => db.TargetUniversityPrograms.AsNoTracking().Select(x => new AdminCatalogRow
            { Id = x.Id, Name = x.Name, Source = x.Source, SourceId = x.SourceId, IsActive = x.IsActive,
                UniversityName = x.UniversityName, ProgramCode = x.ProgramCode, ScoreType = x.ScoreType, MinimumScore = x.MinimumScore, ScoreYear = x.ScoreYear }),
            _ => throw new ArgumentException("Geçersiz katalog türü.")
        };
        if (filter.IsActive.HasValue) query = query.Where(x => x.IsActive == filter.IsActive);
        if (!string.IsNullOrWhiteSpace(filter.Source)) query = query.Where(x => x.Source == filter.Source.Trim());
        if (filter.GradeNumber.HasValue) query = query.Where(x => x.GradeNumber == filter.GradeNumber);
        if (!string.IsNullOrWhiteSpace(filter.ExamCode)) query = query.Where(x => x.ExamCode == filter.ExamCode.Trim());
        if (filter.LessonId.HasValue) query = query.Where(x => x.LessonId == filter.LessonId);
        if (filter.UnitId.HasValue) query = query.Where(x => x.UnitId == filter.UnitId);
        if (filter.ParentId.HasValue) query = query.Where(x => x.ParentId == filter.ParentId);
        if (filter.HasParent.HasValue) query = query.Where(x => (x.ParentId != null) == filter.HasParent.Value);
        if (!string.IsNullOrWhiteSpace(filter.ProvinceId)) query = query.Where(x => x.ProvinceId == filter.ProvinceId.Trim());
        if (!string.IsNullOrWhiteSpace(filter.DistrictId)) query = query.Where(x => x.DistrictId == filter.DistrictId.Trim());
        if (!string.IsNullOrWhiteSpace(filter.ScoreType)) query = query.Where(x => x.ScoreType == filter.ScoreType.Trim());
        if (filter.ScoreYear.HasValue) query = query.Where(x => x.ScoreYear == filter.ScoreYear);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = "%" + term + "%";
            query = kind == CatalogKind.UniversityPrograms
                ? query.Where(x => EF.Functions.ILike(x.Name, pattern, "\\")
                    || EF.Functions.ILike(x.UniversityName!, pattern, "\\")
                    || EF.Functions.ILike(x.ProgramCode!, pattern, "\\"))
                : query.Where(x => EF.Functions.ILike(x.Name, pattern, "\\"));
        }
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(cancellationToken);
        return new(rows, count, filter.PageNumber, filter.PageSize);
    }

    private static void ValidateFilters(CatalogKind kind, AdminCatalogFilter filter)
    {
        var lessons = kind == CatalogKind.Lessons;
        var topics = kind == CatalogKind.Topics;
        var schools = kind == CatalogKind.Schools;
        var universities = kind == CatalogKind.UniversityPrograms;
        if (filter.GradeNumber is < 1 or > 12 || filter.ScoreYear is < 1900 or > 2200
            || filter.LessonId == Guid.Empty || filter.UnitId == Guid.Empty || filter.ParentId == Guid.Empty
            || filter.ExamCode?.Length > 30 || filter.ScoreType?.Length > 30
            || filter.ProvinceId?.Length > 20 || filter.DistrictId?.Length > 20
            || (!lessons && (filter.GradeNumber.HasValue || filter.ExamCode is not null))
            || (kind is not (CatalogKind.Units or CatalogKind.Topics) && filter.LessonId.HasValue)
            || (!topics && (filter.UnitId.HasValue || filter.ParentId.HasValue || filter.HasParent.HasValue))
            || (!schools && (filter.ProvinceId is not null || filter.DistrictId is not null))
            || (filter.DistrictId is not null && string.IsNullOrWhiteSpace(filter.ProvinceId))
            || (!universities && filter.ScoreType is not null)
            || (!(schools || universities) && filter.ScoreYear.HasValue))
            throw new ArgumentException("Seçilen katalog için filtre değerlerini kontrol edin.");
    }
}
