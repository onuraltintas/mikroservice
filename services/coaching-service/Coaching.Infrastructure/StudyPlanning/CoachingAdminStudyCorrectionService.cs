using System.Text.Encodings.Web;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Infrastructure.Middleware;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed record AdminPlanCorrection(int ExpectedVersion, string Title, bool Archive, string Reason);
public sealed record AdminTaskCorrection(int ExpectedPlanVersion, string Title, DateOnly PlannedDate, int PlannedMinutes, string Reason);

public sealed class CoachingAdminStudyCorrectionService(CoachingDbContext db, ICoachingAdminScopeAuthorization scope, ICurrentUserService user)
{
    public Task CorrectPlanAsync(Guid student, Guid revisionId, AdminPlanCorrection request, CancellationToken ct)
        => WriteAsync(student, revisionId, request.ExpectedVersion, request.Reason, async plan =>
        {
            var before = new { plan.Title, plan.Status };
            if (plan.Status == StudyPlanStatus.Archived) throw Conflict("Arşivlenmiş plan değiştirilemez.");
            plan.CorrectTitle(request.Title);
            if (request.Archive) plan.Archive();
            return (before, new { plan.Title, plan.Status }, "StudyPlanRevision", revisionId);
        }, ct);

    public Task CorrectTaskAsync(Guid student, Guid revisionId, Guid taskId, AdminTaskCorrection request, CancellationToken ct)
        => WriteAsync(student, revisionId, request.ExpectedPlanVersion, request.Reason, async plan =>
        {
            if (plan.Status == StudyPlanStatus.Archived) throw Conflict("Arşivlenmiş plan değiştirilemez.");
            var task = await db.StudyPlanTasks.SingleOrDefaultAsync(x => x.Id == taskId && x.StudentId == student && x.RevisionId == revisionId, ct)
                ?? throw new BusinessRuleException("StudyPlanning.NotFound", "Görev bulunamadı.");
            if (task.IsCompleted) throw Conflict("Tamamlanmış çalışma değiştirilemez.");
            var before = new { task.Title, task.PlannedDate, task.PlannedMinutes };
            task.CorrectPlanning(request.Title, request.PlannedDate, request.PlannedMinutes);
            plan.CorrectTitle(plan.Title); // Advance the revision version shared with student writes.
            return (before, new { task.Title, task.PlannedDate, task.PlannedMinutes }, "StudyPlanTask", taskId);
        }, ct);

    private async Task WriteAsync(Guid student, Guid revisionId, int version, string reason,
        Func<StudyPlanRevision, Task<(object Before, object After, string Type, Guid Id)>> change, CancellationToken ct)
    {
        if (!(await scope.RequireReadScopeAsync(ct)).IsGlobal || user.UserId is null
            || !user.Roles.Contains("SystemAdmin") || user.User?.HasClaim("permission", PlatformPermissions.Coaching.Manage) != true)
            throw new BusinessRuleException("Authorization.Forbidden", "Öğrenci düzeltmesi için global yönetici ve Koçluk yönetim izni gerekir.");
        if (student == Guid.Empty || revisionId == Guid.Empty || version < 0 || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 200)
            throw new ArgumentException("Güncel sürüm ve 5-200 karakterlik gerekçe gereklidir.");
        if (db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Correction requires a dedicated context.");
        var auditId = Guid.NewGuid();
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (await db.AdminAuditRecords.AsNoTracking().AnyAsync(x => x.Id == auditId, ct)) return;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", ct);
                var key = "coaching-study-plan:" + student;
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
                var plan = await db.StudyPlanRevisions.SingleOrDefaultAsync(x => x.Id == revisionId && x.StudentId == student, ct)
                    ?? throw new BusinessRuleException("StudyPlanning.NotFound", "Plan bulunamadı.");
                if (plan.Version != version) throw Conflict("Plan değişmiş. Güncel kaydı açıp yeniden deneyin.");
                var result = await change(plan);
                var payload = JsonSerializer.Serialize(new { studentId = student, revisionId, reason = reason.Trim(), before = result.Before, after = result.After },
                    new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                if (payload.Length > 2000) throw new ArgumentException("Denetim kaydı sınırı aşıldı; metindeki kontrol karakterlerini kaldırın.");
                db.AdminAuditRecords.Add(new(auditId, DateTimeOffset.UtcNow, "Coaching", user.UserId!.Value.ToString(), "SystemAdmin", null,
                    "PUT", $"/api/coaching-admin/students/{student}/study", 200, auditId.ToString("N"), null, null,
                    "StudentStudyCorrection", result.Type, result.Id.ToString(), payload));
                await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            }
            finally { db.ChangeTracker.Clear(); }
        });
    }
    private static BusinessRuleException Conflict(string message) => new("StudyPlanning.Conflict", message);
}
