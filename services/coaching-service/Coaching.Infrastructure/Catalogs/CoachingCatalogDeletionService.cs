using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using Coaching.Application.Authorization;
using Coaching.Application.CatalogAdministration;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Infrastructure.Middleware;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Catalogs;

public sealed class CoachingCatalogDeletionService(CoachingDbContext db,
    ICoachingAdminScopeAuthorization scope, ICurrentUserService user) : ICoachingCatalogDeletionService
{
    public async Task<CatalogUsage> GetUsageAsync(CatalogKind kind, Guid id, CancellationToken cancellationToken)
    {
        await RequireGlobalAsync(cancellationToken);
        var entity = await LoadAsync(kind, id, cancellationToken);
        return await UsageAsync(kind, id, entity, cancellationToken);
    }

    public async Task DeleteAsync(CatalogKind kind, Guid id, CatalogDeleteRequest request, CancellationToken cancellationToken)
    {
        await RequireGlobalAsync(cancellationToken);
        if (id == Guid.Empty || request.ConfirmId != id || string.IsNullOrWhiteSpace(request.Reason)
            || request.Reason.Trim().Length is < 5 or > 500 || request.Fingerprint?.Length != 64)
            throw new ArgumentException("Kalıcı silme için kayıt onayı ve 5–500 karakterlik gerekçe gereklidir.");
        var auditId = Guid.NewGuid();
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // A stable transactional audit ID proves success if the commit acknowledgement was lost.
            if (await db.AdminAuditRecords.AsNoTracking().AnyAsync(x => x.Id == auditId, cancellationToken)) return;
            object? entity = null;
            AdminAuditRecord? audit = null;
            try
            {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // A rare administrative delete must exclude concurrent reference creation,
            // including exam JSON references which have no database foreign key.
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("""
                LOCK TABLE coaching.academic_goals, coaching.exam_results,
                    coaching.study_catalog_lessons, coaching.study_catalog_topics, coaching.study_catalog_units,
                    coaching.study_plan_tasks, coaching.target_schools, coaching.target_university_programs
                IN SHARE ROW EXCLUSIVE MODE
                """, cancellationToken);
            entity = await LoadAsync(kind, id, cancellationToken);
            var usage = await UsageAsync(kind, id, entity, cancellationToken);
            if (!string.Equals(request.Fingerprint, usage.Fingerprint, StringComparison.Ordinal))
                throw new BusinessRuleException("Catalog.Stale", "Kayıt değişmiş. Güncel kaydı inceleyip yeniden onaylayın.");
            if (!usage.CanDelete)
                throw new BusinessRuleException("Catalog.InUse", "Kayıt kullanımda; geçmişi korumak için kalıcı silinemez. Pasife alabilirsiniz.");
            db.Remove(entity);
            var snapshot = JsonSerializer.SerializeToElement(entity, entity.GetType());
            audit = new(auditId, DateTimeOffset.UtcNow, "Coaching",
                user.UserId!.Value.ToString(), "SystemAdmin", null, "DELETE", $"/api/coaching-admin/catalog/{kind}/{id}",
                200, Guid.NewGuid().ToString("N"), null, null, "CatalogPermanentDelete", kind.ToString(), id.ToString(),
                JsonSerializer.Serialize(new { reason = request.Reason.Trim(), fingerprint = usage.Fingerprint,
                    name = usage.Name, source = snapshot.GetProperty("Source").GetString(), sourceId = snapshot.GetProperty("SourceId").GetString() },
                    new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            db.AdminAuditRecords.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            }
            finally
            {
                // Detach only this attempt's entities; unrelated tracked work is never cleared.
                if (entity is not null) db.Entry(entity).State = EntityState.Detached;
                if (audit is not null) db.Entry(audit).State = EntityState.Detached;
            }
        });
    }

    private async Task RequireGlobalAsync(CancellationToken cancellationToken)
    {
        if (!(await scope.RequireReadScopeAsync(cancellationToken)).IsGlobal || !user.UserId.HasValue)
            throw new BusinessRuleException("Authorization.Forbidden", "Ortak katalog yönetimi yalnız global yöneticiye açıktır.");
    }

    private async Task<object> LoadAsync(CatalogKind kind, Guid id, CancellationToken cancellationToken)
    {
        object? entity = kind switch
        {
            CatalogKind.Lessons => await db.StudyCatalogLessons.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken),
            CatalogKind.Units => await db.StudyCatalogUnits.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken),
            CatalogKind.Topics => await db.StudyCatalogTopics.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken),
            CatalogKind.Schools => await db.TargetSchools.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken),
            CatalogKind.UniversityPrograms => await db.TargetUniversityPrograms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken),
            _ => throw new ArgumentException("Geçerli bir katalog türü seçin.")
        };
        return entity ?? throw new BusinessRuleException("Catalog.NotFound", "Katalog kaydı bulunamadı.");
    }

    private async Task<CatalogUsage> UsageAsync(CatalogKind kind, Guid id, object entity, CancellationToken cancellationToken)
    {
        var serialized = JsonSerializer.Serialize(entity, entity.GetType());
        using var json = JsonDocument.Parse(serialized);
        var name = json.RootElement.GetProperty("Name").GetString()!;
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));
        var catalog = kind switch
        {
            CatalogKind.Lessons => await db.StudyCatalogUnits.CountAsync(x => x.LessonId == id, cancellationToken)
                + await db.StudyCatalogTopics.CountAsync(x => x.LessonId == id, cancellationToken),
            CatalogKind.Units => await db.StudyCatalogTopics.CountAsync(x => x.UnitId == id, cancellationToken),
            CatalogKind.Topics => await db.StudyCatalogTopics.CountAsync(x => x.ParentId == id, cancellationToken),
            _ => 0
        };
        var plans = kind == CatalogKind.Topics
            ? await db.StudyPlanTasks.CountAsync(x => x.TopicId == id, cancellationToken) : 0;
        var goals = kind switch
        {
            CatalogKind.Schools => await db.AcademicGoals.CountAsync(x => x.TargetSchoolId == id, cancellationToken),
            CatalogKind.UniversityPrograms => await db.AcademicGoals.CountAsync(x => x.TargetUniversityProgramId == id, cancellationToken),
            _ => 0
        };
        // Conservative matching preserves all stored JSON history, including formatting/casing variants.
        var pattern = "%" + id.ToString() + "%";
        var exams = kind is CatalogKind.Lessons or CatalogKind.Topics
            ? await db.Database.SqlQuery<int>($"""
                SELECT count(*)::integer AS "Value" FROM coaching.exam_results
                WHERE lesson_answers::text ILIKE {pattern}
                """).SingleAsync(cancellationToken) : 0;
        return new(id, name, fingerprint, catalog, plans, goals, exams);
    }
}
