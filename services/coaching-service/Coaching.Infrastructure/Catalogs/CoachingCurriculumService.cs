using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Infrastructure.Middleware;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coaching.Infrastructure.Catalogs;

public sealed record CurriculumCreateRequest(string Code, string Name, string VersionLabel, int ValidFromYear,
    int ValidToYear, string Source, string? SourceUrl, IReadOnlyList<Guid> LessonIds, string Reason);
public sealed record CurriculumPublishRequest(int ExpectedVersion, bool IsPublished, string Reason);

public sealed class CoachingCurriculumService(CoachingDbContext db, ICoachingAdminScopeAuthorization scope, ICurrentUserService user)
{
    public async Task<IReadOnlyList<CurriculumVersion>> ListAsync(CancellationToken ct)
    { await RequireGlobal(ct); return await db.CurriculumVersions.AsNoTracking().OrderByDescending(x => x.ValidFromYear).ThenBy(x => x.Code).ToArrayAsync(ct); }

    public async Task<CurriculumVersion> CreateAsync(CurriculumCreateRequest r, CancellationToken ct)
    {
        await RequireGlobal(ct); ValidateReason(r.Reason);
        var curriculum = CurriculumVersion.Create(r.Code, r.Name, r.VersionLabel, r.ValidFromYear, r.ValidToYear, r.Source, r.SourceUrl);
        if (r.LessonIds is null || r.LessonIds.Count is < 1 or > 500 || r.LessonIds.Distinct().Count() != r.LessonIds.Count)
            throw new ArgumentException("1-500 farklı ders seçin.");
        if (await db.CurriculumVersions.AnyAsync(x => x.Code == curriculum.Code, ct)) throw new ArgumentException("Program kodu zaten var.");
        var ids = r.LessonIds.ToArray();
        var lessons = await db.StudyCatalogLessons.Where(x => Enumerable.Contains(ids, x.Id)).ToArrayAsync(ct);
        if (lessons.Length != ids.Length || lessons.Any(x => x.CurriculumVersionId.HasValue))
            throw new ArgumentException("Yalnız henüz program sürümüne bağlanmamış dersler seçilebilir. Yeni sürüm için yeni ders kayıtları kullanın.");
        foreach (var lesson in lessons)
        {
            lesson.AssignCurriculum(curriculum.Id);
        }
        curriculum.RecordActor(user.UserId!.Value);
        db.CurriculumVersions.Add(curriculum); Audit(curriculum.Id, "CurriculumCreate", r.Reason);
        await Save(ct); return curriculum;
    }

    public async Task<CurriculumVersion> SetPublishedAsync(Guid id, CurriculumPublishRequest r, CancellationToken ct)
    {
        await RequireGlobal(ct); ValidateReason(r.Reason);
        var curriculum = await db.CurriculumVersions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Program sürümü bulunamadı.");
        if (curriculum.Version != r.ExpectedVersion) throw Conflict();
        if (r.IsPublished && !await db.StudyCatalogLessons.AnyAsync(x => x.CurriculumVersionId == id && x.IsActive, ct))
            throw new ArgumentException("Yayınlamak için en az bir aktif ders gereklidir.");
        curriculum.SetPublished(r.IsPublished); curriculum.RecordActor(user.UserId!.Value);
        Audit(id, "CurriculumPublish", r.Reason); await Save(ct); return curriculum;
    }

    private async Task RequireGlobal(CancellationToken ct)
    { if (!(await scope.RequireReadScopeAsync(ct)).IsGlobal || !user.UserId.HasValue) throw new BusinessRuleException("Authorization.Forbidden", "Program sürümlerini yalnız global yönetici yönetebilir."); }
    private static void ValidateReason(string reason)
    { if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 500) throw new ArgumentException("5-500 karakterlik işlem gerekçesi belirtin."); }
    private void Audit(Guid id, string action, string reason) => db.AdminAuditRecords.Add(new AdminAuditRecord(
        Guid.NewGuid(), DateTimeOffset.UtcNow, "Coaching", user.UserId!.Value.ToString(), string.Join(',', user.Roles),
        null, "COMMAND", "/api/coaching-admin/curricula", 200, Guid.NewGuid().ToString(), null, null,
        action, "CurriculumVersion", id.ToString(), System.Text.Json.JsonSerializer.Serialize(new { reason = reason.Trim() })));
    private async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" }) { throw Conflict(); }
    }
    private static BusinessRuleException Conflict() => new("StudyPlanning.Conflict", "Program sürümü başka bir işlemde değişti. Yeniden yükleyin.");
}
