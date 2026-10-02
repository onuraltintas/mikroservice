using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using Coaching.Application.Authorization;
using Coaching.Application.CatalogAdministration;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Infrastructure.Middleware;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Catalogs;

public sealed class CoachingCatalogManagementService(CoachingDbContext db, ICoachingAdminScopeAuthorization scope,
    ICurrentUserService user, ICoachingLocationDirectory locations) : ICoachingCatalogManagementService
{
    public async Task<CatalogEditDocument> GetAsync(CatalogKind kind, Guid id, CancellationToken ct)
    {
        await RequireGlobalAsync(ct);
        return Document(await LoadAsync(kind, id, ct));
    }

    public async Task<CatalogEditDocument> CreateAsync(CatalogKind kind, CatalogSaveRequest request, CancellationToken ct)
    {
        await RequireGlobalAsync(ct);
        Validate(kind, request);
        if (request.Fingerprint is not null) throw new ArgumentException("Yeni kayıtta eski kayıt parmak izi kullanılmaz.");
        var sourceId = Guid.NewGuid().ToString("N");
        // Identity API access happens before taking database locks.
        var location = kind == CatalogKind.Schools ? await ResolveLocationAsync(request, true, ct) : null;
        return await WriteAsync(kind, null, null, "CatalogCreate", request.Reason, async () =>
        {
            await ValidateOwnershipAsync(kind, request, ct);
            return kind switch
            {
                CatalogKind.Lessons => StudyCatalogLesson.Create("admin-manual", sourceId, request.Name, request.GradeNumber, request.ExamCode),
                CatalogKind.Units => StudyCatalogUnit.Create("admin-manual", sourceId, request.LessonId!.Value, request.Name, request.DisplayOrder),
                CatalogKind.Topics => StudyCatalogTopic.Create("admin-manual", sourceId, request.LessonId!.Value, request.UnitId!.Value, request.Name, request.ParentId, request.DisplayOrder, request.EstimatedMinutes),
                CatalogKind.Schools => CreateSchool(sourceId, request, location!),
                CatalogKind.UniversityPrograms => TargetUniversityProgram.Create("admin-manual", sourceId, request.UniversityName!, request.Name, request.ProgramCode, request.ScoreType, request.MinimumScore, request.ScoreYear),
                _ => throw new ArgumentException("Geçerli bir katalog türü seçin.")
            };
        }, ct);
    }

    public async Task<CatalogEditDocument> UpdateAsync(CatalogKind kind, Guid id, CatalogSaveRequest request, CancellationToken ct)
    {
        await RequireGlobalAsync(ct);
        Validate(kind, request);
        var location = kind == CatalogKind.Schools ? await ResolveLocationAsync(request, false, ct) : null;
        return await WriteAsync(kind, id, request.Fingerprint, "CatalogUpdate", request.Reason, async () =>
        {
            var entity = await LoadAsync(kind, id, ct);
            switch (entity)
            {
                case StudyCatalogLesson lesson: lesson.Edit(request.Name, request.GradeNumber, request.ExamCode); break;
                case StudyCatalogUnit unit:
                    if (request.LessonId != unit.LessonId) throw new ArgumentException("Ünitenin ders bağlantısı değiştirilemez; yeni kayıt oluşturun.");
                    unit.Edit(request.Name, request.DisplayOrder); break;
                case StudyCatalogTopic topic:
                    if (request.LessonId != topic.LessonId || request.UnitId != topic.UnitId || request.ParentId != topic.ParentId)
                        throw new ArgumentException("Konunun tarihsel ders/ünite/üst konu bağlantısı değiştirilemez; yeni kayıt oluşturun.");
                    topic.Edit(request.Name, request.DisplayOrder, request.EstimatedMinutes); break;
                case TargetSchool school:
                    school.Edit(request.Name, request.MinimumScore, request.ScoreYear);
                    if (location is not null) school.SetVerifiedLocation(location.Province.Id, location.District.Id);
                    break;
                case TargetUniversityProgram program:
                    program.Edit(request.UniversityName!, request.Name, request.ProgramCode, request.ScoreType, request.MinimumScore, request.ScoreYear); break;
            }
            return entity;
        }, ct);
    }

    public async Task<CatalogEditDocument> SetActiveAsync(CatalogKind kind, Guid id, CatalogStatusRequest request, CancellationToken ct)
    {
        await RequireGlobalAsync(ct);
        return await WriteAsync(kind, id, request.Fingerprint, "CatalogStatus", request.Reason, async () =>
        {
            var entity = await LoadAsync(kind, id, ct);
            await ValidateStatusAsync(entity, request.IsActive, ct);
            switch (entity)
            {
                case StudyCatalogLesson lesson: lesson.SetActive(request.IsActive); break;
                case StudyCatalogUnit unit: unit.SetActive(request.IsActive); break;
                case StudyCatalogTopic topic: topic.SetActive(request.IsActive); break;
                case TargetSchool school: school.SetActive(request.IsActive); break;
                case TargetUniversityProgram program: program.SetActive(request.IsActive); break;
            }
            return entity;
        }, ct);
    }

    private async Task<CatalogEditDocument> WriteAsync(CatalogKind kind, Guid? id, string? fingerprint, string action,
        string reason, Func<Task<object>> change, CancellationToken ct)
    {
        ValidateReason(reason);
        if (id.HasValue && (id == Guid.Empty || fingerprint?.Length != 64)) throw new ArgumentException("Güncel kayıt parmak izi gereklidir.");
        var auditId = Guid.NewGuid();
        CatalogEditDocument? result = null;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (await db.AdminAuditRecords.AsNoTracking().AnyAsync(x => x.Id == auditId, ct)) return;
            object? entity = null;
            AdminAuditRecord? audit = null;
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", ct);
                await db.Database.ExecuteSqlRawAsync("""
                    LOCK TABLE coaching.study_catalog_lessons, coaching.study_catalog_topics,
                        coaching.study_catalog_units, coaching.target_schools, coaching.target_university_programs
                    IN SHARE ROW EXCLUSIVE MODE
                    """, ct);
                CatalogEditDocument? before = null;
                if (id.HasValue)
                {
                    before = Document(await LoadAsync(kind, id.Value, ct));
                    if (before.Fingerprint != fingerprint) throw new BusinessRuleException("Catalog.Stale", "Kayıt değişmiş. Güncel kaydı açıp yeniden düzenleyin.");
                }
                entity = await change();
                result = Document(entity);
                var recordId = result.Data.GetProperty("Id").GetGuid();
                // Source identifiers are immutable and survive in audit even after permanent deletion.
                var payload = JsonSerializer.Serialize(new { reason = reason.Trim(), before = before?.Fingerprint,
                    after = result.Fingerprint, name = result.Data.GetProperty("Name").GetString(),
                    source = result.Data.GetProperty("Source").GetString(), sourceId = result.Data.GetProperty("SourceId").GetString() },
                    new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                if (payload.Length > 2000) throw new ArgumentException("Gerekçe ve kayıt adı denetim sınırını aşıyor; kontrol karakterlerini kaldırın.");
                audit = new(auditId, DateTimeOffset.UtcNow, "Coaching", user.UserId!.Value.ToString(), "SystemAdmin", null,
                    id.HasValue ? "PUT" : "POST", $"/api/coaching-admin/catalog/{kind}/{recordId}", id.HasValue ? 200 : 201,
                    Guid.NewGuid().ToString("N"), null, null, action, kind.ToString(), recordId.ToString(), payload);
                if (id.HasValue) db.Update(entity); else db.Add(entity);
                db.AdminAuditRecords.Add(audit);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            finally
            {
                if (entity is not null) db.Entry(entity).State = EntityState.Detached;
                if (audit is not null) db.Entry(audit).State = EntityState.Detached;
            }
        });
        return result!;
    }

    private async Task RequireGlobalAsync(CancellationToken ct)
    {
        if (!(await scope.RequireReadScopeAsync(ct)).IsGlobal || !user.UserId.HasValue)
            throw new BusinessRuleException("Authorization.Forbidden", "Ortak katalog yalnız global yönetici tarafından değiştirilebilir.");
    }

    private async Task<object> LoadAsync(CatalogKind kind, Guid id, CancellationToken ct)
    {
        object? entity = kind switch
        {
            CatalogKind.Lessons => await db.StudyCatalogLessons.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct),
            CatalogKind.Units => await db.StudyCatalogUnits.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct),
            CatalogKind.Topics => await db.StudyCatalogTopics.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct),
            CatalogKind.Schools => await db.TargetSchools.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct),
            CatalogKind.UniversityPrograms => await db.TargetUniversityPrograms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct),
            _ => throw new ArgumentException("Geçerli bir katalog türü seçin.")
        };
        return entity ?? throw new BusinessRuleException("Catalog.NotFound", "Katalog kaydı bulunamadı.");
    }

    private static CatalogEditDocument Document(object entity)
    {
        // PostgreSQL numeric(10,4) pads decimal scale; equal scores must have equal fingerprints.
        var json = JsonSerializer.Serialize(entity, entity.GetType(), new JsonSerializerOptions { Converters = { new CanonicalDecimalConverter() } });
        return new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))), JsonSerializer.SerializeToElement(entity, entity.GetType()));
    }

    private sealed class CanonicalDecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDecimal();
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
            => writer.WriteRawValue(value.ToString("G29", CultureInfo.InvariantCulture));
    }

    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 500)
            throw new ArgumentException("5-500 karakterlik işlem gerekçesi gereklidir.");
    }

    private static void Validate(CatalogKind kind, CatalogSaveRequest r)
    {
        ValidateReason(r.Reason);
        var lesson = kind == CatalogKind.Lessons;
        var topic = kind == CatalogKind.Topics;
        var university = kind == CatalogKind.UniversityPrograms;
        var school = kind == CatalogKind.Schools;
        if (!Enum.IsDefined(kind) || r.ScoreYear is < 1900 or > 2200
            || (r.MinimumScore.HasValue && (r.MinimumScore < 0 || r.MinimumScore > 999999.9999m || decimal.Round(r.MinimumScore.Value, 4) != r.MinimumScore))
            || r.LessonId == Guid.Empty || r.UnitId == Guid.Empty || r.ParentId == Guid.Empty
            || (!lesson && (r.GradeNumber.HasValue || r.ExamCode is not null))
            || (kind is not (CatalogKind.Units or CatalogKind.Topics) && (r.LessonId.HasValue || r.DisplayOrder.HasValue))
            || (!topic && (r.UnitId.HasValue || r.ParentId.HasValue || r.EstimatedMinutes.HasValue))
            || (!university && (r.UniversityName is not null || r.ProgramCode is not null || r.ScoreType is not null))
            || (!(school || university) && (r.MinimumScore.HasValue || r.ScoreYear.HasValue))
            || (!school && (r.ProvinceId is not null || r.DistrictId is not null)))
            throw new ArgumentException("Seçilen katalog için alan değerlerini kontrol edin.");
    }

    private async Task ValidateOwnershipAsync(CatalogKind kind, CatalogSaveRequest r, CancellationToken ct)
    {
        if (kind is CatalogKind.Units or CatalogKind.Topics && (!r.LessonId.HasValue || !await db.StudyCatalogLessons.AnyAsync(x => x.Id == r.LessonId, ct)))
            throw new ArgumentException("Geçerli bir ders seçin.");
        if (kind == CatalogKind.Topics && (!r.UnitId.HasValue || !await db.StudyCatalogUnits.AnyAsync(x => x.Id == r.UnitId && x.LessonId == r.LessonId, ct)
            || (r.ParentId.HasValue && !await db.StudyCatalogTopics.AnyAsync(x => x.Id == r.ParentId && x.UnitId == r.UnitId && x.LessonId == r.LessonId && x.ParentId == null, ct))))
            throw new ArgumentException("Aynı derse ait ünite ve ana konu seçin.");
    }

    private async Task ValidateStatusAsync(object entity, bool active, CancellationToken ct)
    {
        var invalid = entity switch
        {
            StudyCatalogLesson l when !active => await db.StudyCatalogUnits.AnyAsync(x => x.LessonId == l.Id && x.IsActive, ct) || await db.StudyCatalogTopics.AnyAsync(x => x.LessonId == l.Id && x.IsActive, ct),
            StudyCatalogUnit u when active => !await db.StudyCatalogLessons.AnyAsync(x => x.Id == u.LessonId && x.IsActive, ct),
            StudyCatalogUnit u => await db.StudyCatalogTopics.AnyAsync(x => x.UnitId == u.Id && x.IsActive, ct),
            StudyCatalogTopic t when active => !await db.StudyCatalogLessons.AnyAsync(x => x.Id == t.LessonId && x.IsActive, ct)
                || !await db.StudyCatalogUnits.AnyAsync(x => x.Id == t.UnitId && x.IsActive, ct)
                || (t.ParentId.HasValue && !await db.StudyCatalogTopics.AnyAsync(x => x.Id == t.ParentId && x.IsActive, ct)),
            StudyCatalogTopic t => await db.StudyCatalogTopics.AnyAsync(x => x.ParentId == t.Id && x.IsActive, ct),
            _ => false
        };
        if (invalid) throw new BusinessRuleException("Catalog.Hierarchy", "Önce üst kayıtları etkinleştirin veya bağlı alt kayıtları pasife alın.");
    }

    private sealed record SchoolLocation(LocationProvince Province, LocationDistrict District);
    private async Task<SchoolLocation?> ResolveLocationAsync(CatalogSaveRequest r, bool required, CancellationToken ct)
    {
        if (!required && r.ProvinceId is null && r.DistrictId is null) return null;
        if (string.IsNullOrWhiteSpace(r.ProvinceId) || string.IsNullOrWhiteSpace(r.DistrictId) || r.ProvinceId.Length > 20 || r.DistrictId.Length > 20)
            throw new ArgumentException("Şehir ve ilçeyi birlikte seçin.");
        var provinces = (await locations.GetProvincesAsync(ct)).Where(x => x.Id == r.ProvinceId).ToList();
        var districts = (await locations.GetDistrictsAsync(r.ProvinceId, ct)).Where(x => x.Id == r.DistrictId && x.ProvinceId == r.ProvinceId).ToList();
        if (provinces.Count != 1 || districts.Count != 1) throw new ArgumentException("Ortak konum dizininden geçerli bir şehir/ilçe seçin.");
        return new(provinces[0], districts[0]);
    }

    private static TargetSchool CreateSchool(string sourceId, CatalogSaveRequest r, SchoolLocation location)
    {
        var school = TargetSchool.Create("admin-manual", sourceId, r.Name, location.Province.Name, location.District.Name, r.MinimumScore, r.ScoreYear);
        school.SetVerifiedLocation(location.Province.Id, location.District.Id);
        return school;
    }
}
