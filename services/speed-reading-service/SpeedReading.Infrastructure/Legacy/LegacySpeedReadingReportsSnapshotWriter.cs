using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.Progress;
using SpeedReading.Application.Reports;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingReportsSnapshotWriter(SpeedReadingDbContext db)
    : ISpeedReadingReportsSnapshotWriter
{
    private const string CreateScope = "speed-reading.report-snapshots.create";
    private const string DeleteScope = "speed-reading.report-snapshots.delete";

    public async Task<ReportSnapshotDetail> CreateSnapshotAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        CreateReportSnapshotRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, idempotencyKey, request);
        var key = NormalizeKey(idempotencyKey);
        var hash = CreateRequestHash(actorId, CreateScope, Guid.Empty, request);
        var existing = await GetLedgerAsync(CreateScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return await GetDetailAsync(actorId, existing.ResourceId, cancellationToken);
        }

        var template = await GetTemplateForGenerationAsync(
            actorId,
            isGlobalAdministrator,
            request.ReportTemplateId,
            cancellationToken);
        var range = SpeedReadingReportSnapshotRules.ResolveDateRange(
            request.ReportStartDate,
            request.ReportEndDate,
            DateTime.UtcNow);
        var now = DateTime.UtcNow;
        var snapshot = new LegacyReportSnapshot
        {
            Id = Guid.NewGuid(),
            ReportTemplateId = template.Id,
            GeneratedForUserId = actorId,
            GeneratedByUserId = actorId,
            GeneratedAt = now,
            ReportStartDate = range.Start,
            ReportEndDate = range.End,
            DataJson = SpeedReadingReportSnapshotRules.NormalizeDataJson(request.Data),
            CreatedAt = now,
            CreatedBy = actorId
        };

        db.ReportSnapshots.Add(snapshot);
        AddLedger(CreateScope, key, hash, snapshot.Id, now);
        var persistedId = await SaveOrReplayAsync(
            CreateScope,
            key,
            hash,
            snapshot.Id,
            cancellationToken);
        return await GetDetailAsync(actorId, persistedId, cancellationToken);
    }

    public async Task DeleteSnapshotAsync(
        Guid actorId,
        Guid snapshotId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (snapshotId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportSnapshot.InvalidId",
                "Geçerli bir snapshot kimliği gereklidir.");
        }

        var key = NormalizeKey(idempotencyKey);
        var hash = SpeedReadingRequestHasher.Create(
            actorId.ToString("D"),
            DeleteScope,
            snapshotId.ToString("D"));
        var existing = await GetLedgerAsync(DeleteScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return;
        }

        var snapshot = await db.ReportSnapshots.SingleOrDefaultAsync(
            item => item.Id == snapshotId
                && item.GeneratedForUserId == actorId
                && !item.IsDeleted,
            cancellationToken)
            ?? throw new NotFoundException("ReportSnapshot", snapshotId);

        var now = DateTime.UtcNow;
        snapshot.IsDeleted = true;
        snapshot.DeletedAt = now;
        snapshot.DeletedBy = actorId;
        snapshot.UpdatedAt = now;
        snapshot.UpdatedBy = actorId;
        AddLedger(DeleteScope, key, hash, snapshot.Id, now);
        _ = await SaveOrReplayAsync(DeleteScope, key, hash, snapshot.Id, cancellationToken);
    }

    private async Task<LegacyReportTemplate> GetTemplateForGenerationAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var template = await db.ReportTemplates.SingleOrDefaultAsync(
            item => item.Id == templateId && !item.IsDeleted,
            cancellationToken)
            ?? throw new NotFoundException("ReportTemplate", templateId);

        if (!template.IsActive)
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportTemplate.Inactive",
                "Pasif rapor şablonundan snapshot oluşturulamaz.");
        }

        if (!isGlobalAdministrator
            && (template.Type == LegacyReportType.Admin
                || (!template.IsSystemTemplate && template.CreatedByUserId != actorId)))
        {
            throw new BusinessRuleException(
                "Authorization.ReportTemplate.Scope",
                "Bu rapor şablonundan snapshot oluşturma yetkiniz yok.");
        }

        return template;
    }

    private async Task<ReportSnapshotDetail> GetDetailAsync(
        Guid actorId,
        Guid snapshotId,
        CancellationToken cancellationToken) =>
        await (
            from snapshot in db.ReportSnapshots.AsNoTracking()
            join template in db.ReportTemplates.AsNoTracking()
                on snapshot.ReportTemplateId equals template.Id
            where snapshot.Id == snapshotId
                && snapshot.GeneratedForUserId == actorId
                && !snapshot.IsDeleted
            select new ReportSnapshotDetail(
                snapshot.Id,
                snapshot.GeneratedForUserId,
                snapshot.ReportTemplateId,
                template.Name,
                snapshot.GeneratedAt,
                snapshot.ReportStartDate,
                snapshot.ReportEndDate,
                snapshot.DataJson.Length > SpeedReadingReportSnapshotRules.MaxDataJsonLength
                    ? string.Empty
                    : snapshot.DataJson,
                snapshot.DataJson.Length > SpeedReadingReportSnapshotRules.MaxDataJsonLength,
                snapshot.PdfFileUrl,
                snapshot.ExcelFileUrl,
                snapshot.IsViewed,
                snapshot.ViewedAt))
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new BusinessRuleException(
            "Idempotency.ResourceMissing",
            "Idempotency kaydına ait snapshot bulunamadı; yeni bir anahtar kullanın.");

    private async Task<LegacyIdempotencyRecord?> GetLedgerAsync(
        string scope,
        string key,
        CancellationToken cancellationToken) =>
        await db.IdempotencyRecords.SingleOrDefaultAsync(
            item => item.Scope == scope && item.Key == key,
            cancellationToken);

    private void AddLedger(
        string scope,
        string key,
        string requestHash,
        Guid resourceId,
        DateTime createdAt) =>
        db.IdempotencyRecords.Add(new LegacyIdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Scope = scope,
            Key = key,
            RequestHash = requestHash,
            ResourceId = resourceId,
            CreatedAt = createdAt
        });

    private async Task<Guid> SaveOrReplayAsync(
        string scope,
        string key,
        string requestHash,
        Guid resourceId,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return resourceId;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await db.IdempotencyRecords.SingleAsync(
                item => item.Scope == scope && item.Key == key,
                cancellationToken);
            EnsureReplay(concurrent, requestHash);
            return concurrent.ResourceId;
        }
    }

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        object request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"),
            scope,
            resourceId.ToString("D"),
            JsonSerializer.Serialize(request));

    private static void EnsureReplay(LegacyIdempotencyRecord record, string requestHash)
    {
        if (!record.Matches(requestHash))
        {
            throw new BusinessRuleException(
                "IdempotencyKey.Reused",
                "Idempotency-Key farklı bir istek için tekrar kullanılamaz.");
        }
    }

    private static void ValidateRequest(
        Guid actorId,
        string idempotencyKey,
        CreateReportSnapshotRequest request)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (request is null || request.ReportTemplateId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportSnapshot.Request.Invalid",
                "Geçerli bir rapor şablonu gereklidir.");
        }
    }

    private static void ValidateIdempotency(Guid actorId, string key)
    {
        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(key)
            || key.Trim().Length is < 16 or > 128
            || key.Trim().Any(character => !(char.IsLetterOrDigit(character)
                || character is '.' or '_' or '-' or '~')))
        {
            throw new BusinessRuleException(
                "SpeedReading.IdempotencyKey.Invalid",
                "Geçerli bir Idempotency-Key gereklidir.");
        }
    }

    private static string NormalizeKey(string key) => key.Trim();

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
