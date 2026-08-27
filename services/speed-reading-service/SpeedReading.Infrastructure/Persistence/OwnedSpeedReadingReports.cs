using System.Linq.Expressions;
using System.Net.Mail;
using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.Progress;
using SpeedReading.Application.Reports;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingReports(OwnedSpeedReadingDbContext db)
    : ILegacySpeedReadingReports,
      ISpeedReadingReportsAdminWriter,
      ISpeedReadingReportsScheduleWriter,
      ISpeedReadingReportsSnapshotWriter
{
    private const int MaxDataJsonLength = 1_000_000;
    private const int MaxConfigurationLength = 1_000_000;
    private const string TemplateCreateScope = "speed-reading.report-templates.create";
    private const string TemplateUpdateScope = "speed-reading.report-templates.update";
    private const string TemplateDeleteScope = "speed-reading.report-templates.delete";
    private const string ScheduleCreateScope = "speed-reading.report-schedules.create";
    private const string ScheduleUpdateScope = "speed-reading.report-schedules.update";
    private const string ScheduleStatusScope = "speed-reading.report-schedules.status";
    private const string ScheduleDeleteScope = "speed-reading.report-schedules.delete";
    private const string SnapshotCreateScope = "speed-reading.report-snapshots.create";
    private const string SnapshotDeleteScope = "speed-reading.report-snapshots.delete";

    public async Task<IReadOnlyList<ReportTemplateSummary>> GetTemplatesAsync(
        string? type,
        bool? isActive,
        Guid requestingUserId,
        bool isGlobalAdministrator,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = db.ReportTemplates.AsNoTracking().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<LegacyReportType>(type, true, out var reportType))
                throw new BusinessRuleException("SpeedReading.Reports.TemplateType.Invalid", "Geçersiz rapor şablonu türü.");
            query = query.Where(item => item.Type == reportType);
        }
        if (isActive.HasValue)
            query = query.Where(item => item.IsActive == isActive.Value);
        if (!isGlobalAdministrator)
        {
            query = query.Where(item => item.Type != LegacyReportType.Admin
                && (item.IsSystemTemplate || item.CreatedByUserId == requestingUserId));
        }

        return await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.Name)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(ToTemplateSummaryExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<ReportTemplateSummary?> GetTemplateAsync(
        Guid templateId,
        Guid requestingUserId,
        bool isGlobalAdministrator,
        CancellationToken cancellationToken = default) =>
        await db.ReportTemplates
            .AsNoTracking()
            .Where(item => item.Id == templateId
                && !item.IsDeleted
                && (isGlobalAdministrator
                    || (item.Type != LegacyReportType.Admin
                        && (item.IsSystemTemplate || item.CreatedByUserId == requestingUserId))))
            .Select(ToTemplateSummaryExpression())
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ReportSnapshotSummary>> GetUserSnapshotsAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default) =>
        await (
            from snapshot in db.ReportSnapshots.AsNoTracking()
            join template in db.ReportTemplates.AsNoTracking() on snapshot.ReportTemplateId equals template.Id
            where snapshot.GeneratedForUserId == userId && !snapshot.IsDeleted
            orderby snapshot.GeneratedAt descending
            select new ReportSnapshotSummary(
                snapshot.Id,
                snapshot.ReportTemplateId,
                template.Name,
                snapshot.GeneratedAt,
                snapshot.ReportStartDate,
                snapshot.ReportEndDate,
                snapshot.PdfFileUrl,
                snapshot.ExcelFileUrl,
                snapshot.IsViewed,
                snapshot.ViewedAt))
        .Take(Math.Clamp(limit, 1, 100))
        .ToListAsync(cancellationToken);

    public async Task<ReportSnapshotDetail?> GetUserSnapshotAsync(
        Guid userId,
        Guid snapshotId,
        CancellationToken cancellationToken = default) =>
        await (
            from snapshot in db.ReportSnapshots.AsNoTracking()
            join template in db.ReportTemplates.AsNoTracking() on snapshot.ReportTemplateId equals template.Id
            where snapshot.Id == snapshotId && snapshot.GeneratedForUserId == userId && !snapshot.IsDeleted
            select new ReportSnapshotDetail(
                snapshot.Id,
                snapshot.GeneratedForUserId,
                snapshot.ReportTemplateId,
                template.Name,
                snapshot.GeneratedAt,
                snapshot.ReportStartDate,
                snapshot.ReportEndDate,
                snapshot.DataJson.Length > MaxDataJsonLength ? string.Empty : snapshot.DataJson,
                snapshot.DataJson.Length > MaxDataJsonLength,
                snapshot.PdfFileUrl,
                snapshot.ExcelFileUrl,
                snapshot.IsViewed,
                snapshot.ViewedAt))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ScheduledReportSummary>> GetUserScheduledReportsAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default) =>
        await (
            from schedule in db.ScheduledReports.AsNoTracking()
            join template in db.ReportTemplates.AsNoTracking() on schedule.ReportTemplateId equals template.Id
            where schedule.UserId == userId && !schedule.IsDeleted
            orderby schedule.IsActive descending, schedule.NextRunAt
            select ToScheduleSummary(schedule, template.Name))
        .Take(Math.Clamp(limit, 1, 100))
        .ToListAsync(cancellationToken);

    public async Task<ScheduledReportSummary?> GetUserScheduledReportAsync(
        Guid userId,
        Guid scheduleId,
        CancellationToken cancellationToken = default) =>
        await (
            from schedule in db.ScheduledReports.AsNoTracking()
            join template in db.ReportTemplates.AsNoTracking() on schedule.ReportTemplateId equals template.Id
            where schedule.Id == scheduleId && schedule.UserId == userId && !schedule.IsDeleted
            select ToScheduleSummary(schedule, template.Name))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<ReportTemplateSummary> CreateTemplateAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        CreateReportTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateTemplateRequest(actorId, idempotencyKey, request);
        var type = ParseType(request.Type);
        var category = ParseCategory(request.Category);
        EnsureCategoryScope(type, category, isGlobalAdministrator);
        var key = NormalizeKey(idempotencyKey);
        var hash = CreateRequestHash(actorId, TemplateCreateScope, Guid.Empty, request);
        var existing = await GetLedgerAsync(TemplateCreateScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return await GetTemplateForResponseAsync(existing.ResourceId, actorId, isGlobalAdministrator, cancellationToken);
        }

        var now = DateTime.UtcNow;
        var template = new LegacyReportTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Type = type,
            Category = category,
            ConfigurationJson = request.ConfigurationJson.Trim(),
            IsSystemTemplate = false,
            CreatedByUserId = actorId,
            CreatedById = actorId,
            CreatedAt = now,
            CreatedBy = actorId,
            IsActive = true
        };
        db.ReportTemplates.Add(template);
        AddLedger(TemplateCreateScope, key, hash, template.Id, now);
        var persistedId = await SaveOrReplayAsync(TemplateCreateScope, key, hash, template.Id, cancellationToken);
        return await GetTemplateForResponseAsync(persistedId, actorId, isGlobalAdministrator, cancellationToken);
    }

    public async Task<ReportTemplateSummary> UpdateTemplateAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        Guid templateId,
        UpdateReportTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateTemplateRequest(actorId, idempotencyKey, request);
        var key = NormalizeKey(idempotencyKey);
        var hash = CreateRequestHash(actorId, TemplateUpdateScope, templateId, request);
        var existing = await GetLedgerAsync(TemplateUpdateScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return await GetTemplateForResponseAsync(existing.ResourceId, actorId, isGlobalAdministrator, cancellationToken);
        }
        var template = await GetTemplateForWriteAsync(actorId, isGlobalAdministrator, templateId, cancellationToken);
        template.Name = request.Name.Trim();
        template.Description = request.Description.Trim();
        template.ConfigurationJson = request.ConfigurationJson.Trim();
        template.IsActive = request.IsActive;
        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedBy = actorId;
        AddLedger(TemplateUpdateScope, key, hash, template.Id, DateTime.UtcNow);
        var persistedId = await SaveOrReplayAsync(TemplateUpdateScope, key, hash, template.Id, cancellationToken);
        return await GetTemplateForResponseAsync(persistedId, actorId, isGlobalAdministrator, cancellationToken);
    }

    public async Task DeleteTemplateAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        Guid templateId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = NormalizeKey(idempotencyKey);
        var hash = SpeedReadingRequestHasher.Create(actorId.ToString("D"), TemplateDeleteScope, templateId.ToString("D"));
        var existing = await GetLedgerAsync(TemplateDeleteScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return;
        }
        var template = await GetTemplateForWriteAsync(actorId, isGlobalAdministrator, templateId, cancellationToken);
        if (await db.ScheduledReports.AnyAsync(item => item.ReportTemplateId == templateId && item.IsActive && !item.IsDeleted, cancellationToken))
            throw new BusinessRuleException("SpeedReading.ReportTemplate.HasActiveSchedules", "Aktif zamanlanmış raporları olan şablon silinemez.");
        var now = DateTime.UtcNow;
        template.IsDeleted = true;
        template.IsActive = false;
        template.DeletedAt = now;
        template.DeletedBy = actorId;
        template.UpdatedAt = now;
        template.UpdatedBy = actorId;
        AddLedger(TemplateDeleteScope, key, hash, template.Id, now);
        _ = await SaveOrReplayAsync(TemplateDeleteScope, key, hash, template.Id, cancellationToken);
    }

    public async Task<ScheduledReportSummary> CreateScheduledReportAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        CreateScheduledReportRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateScheduleRequest(actorId, idempotencyKey, request);
        var normalized = NormalizeSchedule(request.Frequency, request.DayOfWeek, request.DayOfMonth, request.DeliveryTime, request.SendEmail, request.SaveToDashboard, request.EmailRecipients);
        await EnsureTemplateCanBeScheduledAsync(actorId, isGlobalAdministrator, request.ReportTemplateId, cancellationToken);
        var key = NormalizeKey(idempotencyKey);
        var hash = CreateRequestHash(actorId, ScheduleCreateScope, Guid.Empty, request);
        var existing = await GetLedgerAsync(ScheduleCreateScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return await GetScheduleForResponseAsync(existing.ResourceId, actorId, cancellationToken);
        }
        var now = DateTime.UtcNow;
        var schedule = new LegacyScheduledReport
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            CreatedBy = actorId,
            ReportTemplateId = request.ReportTemplateId,
            UserId = actorId,
            Frequency = normalized.Frequency,
            DayOfWeek = normalized.DayOfWeek,
            DayOfMonth = normalized.DayOfMonth,
            DeliveryTime = normalized.DeliveryTime,
            IsActive = true,
            NextRunAt = CalculateNextRunAt(normalized.Frequency, normalized.DayOfWeek, normalized.DayOfMonth, normalized.DeliveryTime, now),
            SendEmail = normalized.SendEmail,
            SaveToDashboard = normalized.SaveToDashboard,
            EmailRecipients = normalized.EmailRecipients
        };
        db.ScheduledReports.Add(schedule);
        AddLedger(ScheduleCreateScope, key, hash, schedule.Id, now);
        var persistedId = await SaveOrReplayAsync(ScheduleCreateScope, key, hash, schedule.Id, cancellationToken);
        return await GetScheduleForResponseAsync(persistedId, actorId, cancellationToken);
    }

    public async Task<ScheduledReportSummary> UpdateScheduledReportAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        Guid scheduleId,
        UpdateScheduledReportRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateScheduleRequest(actorId, idempotencyKey, request);
        var normalized = NormalizeSchedule(request.Frequency, request.DayOfWeek, request.DayOfMonth, request.DeliveryTime, request.SendEmail, request.SaveToDashboard, request.EmailRecipients);
        var key = NormalizeKey(idempotencyKey);
        var hash = CreateRequestHash(actorId, ScheduleUpdateScope, scheduleId, request);
        var existing = await GetLedgerAsync(ScheduleUpdateScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return await GetScheduleForResponseAsync(existing.ResourceId, actorId, cancellationToken);
        }
        var schedule = await GetOwnedScheduleAsync(actorId, scheduleId, cancellationToken);
        await EnsureTemplateCanBeScheduledAsync(actorId, isGlobalAdministrator, schedule.ReportTemplateId, cancellationToken);
        schedule.Frequency = normalized.Frequency;
        schedule.DayOfWeek = normalized.DayOfWeek;
        schedule.DayOfMonth = normalized.DayOfMonth;
        schedule.DeliveryTime = normalized.DeliveryTime;
        schedule.IsActive = request.IsActive;
        schedule.SendEmail = normalized.SendEmail;
        schedule.SaveToDashboard = normalized.SaveToDashboard;
        schedule.EmailRecipients = normalized.EmailRecipients;
        schedule.NextRunAt = request.IsActive
            ? CalculateNextRunAt(normalized.Frequency, normalized.DayOfWeek, normalized.DayOfMonth, normalized.DeliveryTime, DateTime.UtcNow)
            : null;
        schedule.UpdatedAt = DateTime.UtcNow;
        schedule.UpdatedBy = actorId;
        AddLedger(ScheduleUpdateScope, key, hash, schedule.Id, DateTime.UtcNow);
        var persistedId = await SaveOrReplayAsync(ScheduleUpdateScope, key, hash, schedule.Id, cancellationToken);
        return await GetScheduleForResponseAsync(persistedId, actorId, cancellationToken);
    }

    public async Task<ScheduledReportSummary> UpdateScheduledReportStatusAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        Guid scheduleId,
        UpdateScheduledReportStatusRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateScheduleRequest(actorId, idempotencyKey, request);
        var key = NormalizeKey(idempotencyKey);
        var hash = CreateRequestHash(actorId, ScheduleStatusScope, scheduleId, request);
        var existing = await GetLedgerAsync(ScheduleStatusScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return await GetScheduleForResponseAsync(existing.ResourceId, actorId, cancellationToken);
        }
        var schedule = await GetOwnedScheduleAsync(actorId, scheduleId, cancellationToken);
        await EnsureTemplateCanBeScheduledAsync(actorId, isGlobalAdministrator, schedule.ReportTemplateId, cancellationToken);
        schedule.IsActive = request.IsActive;
        schedule.NextRunAt = request.IsActive ? CalculateNextRunAt(schedule.Frequency, schedule.DayOfWeek, schedule.DayOfMonth, schedule.DeliveryTime, DateTime.UtcNow) : null;
        schedule.UpdatedAt = DateTime.UtcNow;
        schedule.UpdatedBy = actorId;
        AddLedger(ScheduleStatusScope, key, hash, schedule.Id, DateTime.UtcNow);
        var persistedId = await SaveOrReplayAsync(ScheduleStatusScope, key, hash, schedule.Id, cancellationToken);
        return await GetScheduleForResponseAsync(persistedId, actorId, cancellationToken);
    }

    public async Task DeleteScheduledReportAsync(Guid actorId, bool isGlobalAdministrator, Guid scheduleId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = NormalizeKey(idempotencyKey);
        var hash = SpeedReadingRequestHasher.Create(actorId.ToString("D"), ScheduleDeleteScope, scheduleId.ToString("D"));
        var existing = await GetLedgerAsync(ScheduleDeleteScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return;
        }
        var schedule = await GetOwnedScheduleAsync(actorId, scheduleId, cancellationToken);
        var now = DateTime.UtcNow;
        schedule.IsActive = false;
        schedule.IsDeleted = true;
        schedule.DeletedAt = now;
        schedule.DeletedBy = actorId;
        schedule.UpdatedAt = now;
        schedule.UpdatedBy = actorId;
        AddLedger(ScheduleDeleteScope, key, hash, schedule.Id, now);
        _ = await SaveOrReplayAsync(ScheduleDeleteScope, key, hash, schedule.Id, cancellationToken);
    }

    public async Task<ReportSnapshotDetail> CreateSnapshotAsync(Guid actorId, bool isGlobalAdministrator, CreateReportSnapshotRequest request, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ValidateSnapshotRequest(actorId, idempotencyKey, request);
        var key = NormalizeKey(idempotencyKey);
        var hash = CreateRequestHash(actorId, SnapshotCreateScope, Guid.Empty, request);
        var existing = await GetLedgerAsync(SnapshotCreateScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return await GetSnapshotForResponseAsync(actorId, existing.ResourceId, cancellationToken);
        }
        var template = await GetTemplateForGenerationAsync(actorId, isGlobalAdministrator, request.ReportTemplateId, cancellationToken);
        var range = SpeedReadingReportSnapshotRules.ResolveDateRange(request.ReportStartDate, request.ReportEndDate, DateTime.UtcNow);
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
        AddLedger(SnapshotCreateScope, key, hash, snapshot.Id, now);
        var persistedId = await SaveOrReplayAsync(SnapshotCreateScope, key, hash, snapshot.Id, cancellationToken);
        return await GetSnapshotForResponseAsync(actorId, persistedId, cancellationToken);
    }

    public async Task DeleteSnapshotAsync(Guid actorId, Guid snapshotId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (snapshotId == Guid.Empty)
            throw new BusinessRuleException("SpeedReading.ReportSnapshot.InvalidId", "Geçerli bir snapshot kimliği gereklidir.");
        var key = NormalizeKey(idempotencyKey);
        var hash = SpeedReadingRequestHasher.Create(actorId.ToString("D"), SnapshotDeleteScope, snapshotId.ToString("D"));
        var existing = await GetLedgerAsync(SnapshotDeleteScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return;
        }
        var snapshot = await db.ReportSnapshots.SingleOrDefaultAsync(item => item.Id == snapshotId && item.GeneratedForUserId == actorId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ReportSnapshot", snapshotId);
        var now = DateTime.UtcNow;
        snapshot.IsDeleted = true;
        snapshot.DeletedAt = now;
        snapshot.DeletedBy = actorId;
        snapshot.UpdatedAt = now;
        snapshot.UpdatedBy = actorId;
        AddLedger(SnapshotDeleteScope, key, hash, snapshot.Id, now);
        _ = await SaveOrReplayAsync(SnapshotDeleteScope, key, hash, snapshot.Id, cancellationToken);
    }

    private async Task<LegacyReportTemplate> GetTemplateForWriteAsync(Guid actorId, bool isGlobalAdministrator, Guid templateId, CancellationToken cancellationToken)
    {
        var template = await db.ReportTemplates.SingleOrDefaultAsync(item => item.Id == templateId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ReportTemplate", templateId);
        if (!isGlobalAdministrator && (template.Type == LegacyReportType.Admin || (!template.IsSystemTemplate && template.CreatedByUserId != actorId)))
            throw new BusinessRuleException("Authorization.ReportTemplate.Scope", "Bu rapor şablonu üzerinde işlem yapma yetkiniz yok.");
        if (template.IsSystemTemplate && !isGlobalAdministrator)
            throw new BusinessRuleException("Authorization.ReportTemplate.SystemTemplate", "Sistem rapor şablonları yalnızca SystemAdmin tarafından değiştirilebilir.");
        return template;
    }

    private async Task<LegacyReportTemplate> GetTemplateForGenerationAsync(Guid actorId, bool isGlobalAdministrator, Guid templateId, CancellationToken cancellationToken)
    {
        var template = await db.ReportTemplates.SingleOrDefaultAsync(item => item.Id == templateId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ReportTemplate", templateId);
        if (!template.IsActive)
            throw new BusinessRuleException("SpeedReading.ReportTemplate.Inactive", "Pasif rapor şablonundan snapshot oluşturulamaz.");
        if (!isGlobalAdministrator && (template.Type == LegacyReportType.Admin || (!template.IsSystemTemplate && template.CreatedByUserId != actorId)))
            throw new BusinessRuleException("Authorization.ReportTemplate.Scope", "Bu rapor şablonundan snapshot oluşturma yetkiniz yok.");
        return template;
    }

    private async Task EnsureTemplateCanBeScheduledAsync(Guid actorId, bool isGlobalAdministrator, Guid templateId, CancellationToken cancellationToken)
    {
        var template = await db.ReportTemplates.AsNoTracking().SingleOrDefaultAsync(item => item.Id == templateId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ReportTemplate", templateId);
        if (!template.IsActive)
            throw new BusinessRuleException("SpeedReading.ReportTemplate.Inactive", "Pasif rapor şablonu zamanlanamaz.");
        if (!isGlobalAdministrator && (template.Type == LegacyReportType.Admin || (!template.IsSystemTemplate && template.CreatedByUserId != actorId)))
            throw new BusinessRuleException("Authorization.ReportTemplate.Scope", "Bu rapor şablonu üzerinde işlem yapma yetkiniz yok.");
    }

    private async Task<LegacyScheduledReport> GetOwnedScheduleAsync(Guid actorId, Guid scheduleId, CancellationToken cancellationToken) =>
        await db.ScheduledReports.SingleOrDefaultAsync(item => item.Id == scheduleId && item.UserId == actorId && !item.IsDeleted, cancellationToken)
        ?? throw new NotFoundException("ScheduledReport", scheduleId);

    private async Task<ReportTemplateSummary> GetTemplateForResponseAsync(Guid templateId, Guid actorId, bool isGlobalAdministrator, CancellationToken cancellationToken) =>
        await db.ReportTemplates.AsNoTracking()
            .Where(item => item.Id == templateId && !item.IsDeleted
                && (isGlobalAdministrator || (item.Type != LegacyReportType.Admin && (item.IsSystemTemplate || item.CreatedByUserId == actorId))))
            .Select(ToTemplateSummaryExpression())
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new BusinessRuleException("Idempotency.ResourceMissing", "Idempotency kaydına ait rapor şablonu bulunamadı; yeni bir anahtar kullanın.");

    private async Task<ScheduledReportSummary> GetScheduleForResponseAsync(Guid scheduleId, Guid actorId, CancellationToken cancellationToken) =>
        await (from schedule in db.ScheduledReports.AsNoTracking()
               join template in db.ReportTemplates.AsNoTracking() on schedule.ReportTemplateId equals template.Id
               where schedule.Id == scheduleId && schedule.UserId == actorId && !schedule.IsDeleted
               select ToScheduleSummary(schedule, template.Name)).SingleOrDefaultAsync(cancellationToken)
        ?? throw new BusinessRuleException("Idempotency.ResourceMissing", "Idempotency kaydına ait zamanlanmış rapor bulunamadı; yeni bir anahtar kullanın.");

    private async Task<ReportSnapshotDetail> GetSnapshotForResponseAsync(Guid actorId, Guid snapshotId, CancellationToken cancellationToken) =>
        await (from snapshot in db.ReportSnapshots.AsNoTracking()
               join template in db.ReportTemplates.AsNoTracking() on snapshot.ReportTemplateId equals template.Id
               where snapshot.Id == snapshotId && snapshot.GeneratedForUserId == actorId && !snapshot.IsDeleted
               select new ReportSnapshotDetail(
                   snapshot.Id, snapshot.GeneratedForUserId, snapshot.ReportTemplateId, template.Name,
                   snapshot.GeneratedAt, snapshot.ReportStartDate, snapshot.ReportEndDate,
                   snapshot.DataJson.Length > SpeedReadingReportSnapshotRules.MaxDataJsonLength ? string.Empty : snapshot.DataJson,
                   snapshot.DataJson.Length > SpeedReadingReportSnapshotRules.MaxDataJsonLength,
                   snapshot.PdfFileUrl, snapshot.ExcelFileUrl, snapshot.IsViewed, snapshot.ViewedAt))
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new BusinessRuleException("Idempotency.ResourceMissing", "Idempotency kaydına ait snapshot bulunamadı; yeni bir anahtar kullanın.");

    private async Task<OwnedIdempotencyRecord?> GetLedgerAsync(string scope, string key, CancellationToken cancellationToken) =>
        await db.IdempotencyRecords.SingleOrDefaultAsync(item => item.Scope == scope && item.Key == key, cancellationToken);

    private void AddLedger(string scope, string key, string requestHash, Guid resourceId, DateTime createdAt) =>
        db.IdempotencyRecords.Add(new OwnedIdempotencyRecord
        {
            Id = Guid.NewGuid(), Scope = scope, Key = key, RequestHash = requestHash, ResourceId = resourceId, CreatedAt = createdAt
        });

    private async Task<Guid> SaveOrReplayAsync(string scope, string key, string requestHash, Guid resourceId, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return resourceId;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await db.IdempotencyRecords.SingleAsync(item => item.Scope == scope && item.Key == key, cancellationToken);
            EnsureReplay(concurrent, requestHash);
            return concurrent.ResourceId;
        }
    }

    private static Expression<Func<LegacyReportTemplate, ReportTemplateSummary>> ToTemplateSummaryExpression() =>
        item => new ReportTemplateSummary(item.Id, item.Name, item.Description, item.Type.ToString(), item.Category.ToString(), item.ConfigurationJson, item.IsSystemTemplate, item.CreatedByUserId, item.CreatedAt, item.IsActive);

    private static ScheduledReportSummary ToScheduleSummary(LegacyScheduledReport item, string templateName) =>
        new(item.Id, item.ReportTemplateId, templateName, item.Frequency.ToString(), item.DayOfWeek, item.DayOfMonth, item.DeliveryTime, item.IsActive, item.LastRunAt, item.NextRunAt, item.SuccessCount, item.FailureCount, item.SendEmail, item.SaveToDashboard, item.EmailRecipients);

    private static LegacyReportType ParseType(int value) => Enum.IsDefined((LegacyReportType)value) ? (LegacyReportType)value : throw new BusinessRuleException("SpeedReading.ReportTemplate.Type.Invalid", "Geçersiz rapor şablonu türü.");
    private static LegacyReportCategory ParseCategory(int value) => Enum.IsDefined((LegacyReportCategory)value) ? (LegacyReportCategory)value : throw new BusinessRuleException("SpeedReading.ReportTemplate.Category.Invalid", "Geçersiz rapor şablonu kategorisi.");

    private static void EnsureCategoryScope(LegacyReportType type, LegacyReportCategory category, bool isGlobalAdministrator)
    {
        if (isGlobalAdministrator)
            return;
        if (type == LegacyReportType.Admin || category is LegacyReportCategory.Institution or LegacyReportCategory.PlatformUsage or LegacyReportCategory.ContentAnalysis or LegacyReportCategory.SystemHealth)
            throw new BusinessRuleException("Authorization.ReportTemplate.AdminScope", "Yönetim rapor şablonları yalnızca SystemAdmin tarafından yönetilebilir.");
    }

    private static void ValidateTemplateRequest(Guid actorId, string key, CreateReportTemplateRequest request)
    {
        ValidateIdempotency(actorId, key);
        if (request is null)
            throw new BusinessRuleException("SpeedReading.ReportTemplate.Request.Invalid", "Rapor şablonu isteği gereklidir.");
        ValidateTemplateFields(request.Name, request.Description, request.ConfigurationJson);
    }

    private static void ValidateTemplateRequest(Guid actorId, string key, UpdateReportTemplateRequest request)
    {
        ValidateIdempotency(actorId, key);
        if (request is null)
            throw new BusinessRuleException("SpeedReading.ReportTemplate.Request.Invalid", "Rapor şablonu isteği gereklidir.");
        ValidateTemplateFields(request.Name, request.Description, request.ConfigurationJson);
    }

    private static void ValidateTemplateFields(string? name, string? description, string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new BusinessRuleException("SpeedReading.ReportTemplate.Name.Invalid", "Rapor şablonu adı 1-200 karakter olmalıdır.");
        if (description?.Trim().Length > 1_000)
            throw new BusinessRuleException("SpeedReading.ReportTemplate.Description.Invalid", "Rapor şablonu açıklaması en fazla 1000 karakter olabilir.");
        if (string.IsNullOrWhiteSpace(configurationJson) || configurationJson.Length > MaxConfigurationLength || !IsJsonObjectOrArray(configurationJson))
            throw new BusinessRuleException("SpeedReading.ReportTemplate.Configuration.Invalid", "Rapor şablonu yapılandırması geçerli bir JSON nesnesi veya dizisi olmalıdır.");
    }

    private static bool IsJsonObjectOrArray(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ValidateScheduleRequest(Guid actorId, string key, CreateScheduledReportRequest request)
    {
        ValidateIdempotency(actorId, key);
        if (request is null || request.ReportTemplateId == Guid.Empty)
            throw new BusinessRuleException("SpeedReading.ReportSchedule.Request.Invalid", "Geçerli bir rapor şablonu gereklidir.");
    }

    private static void ValidateScheduleRequest(Guid actorId, string key, UpdateScheduledReportRequest request)
    {
        ValidateIdempotency(actorId, key);
    }

    private static void ValidateScheduleRequest(Guid actorId, string key, UpdateScheduledReportStatusRequest request) => ValidateIdempotency(actorId, key);

    private static (LegacyReportFrequency Frequency, DayOfWeek? DayOfWeek, int? DayOfMonth, TimeSpan DeliveryTime, bool SendEmail, bool SaveToDashboard, string? EmailRecipients) NormalizeSchedule(int frequency, DayOfWeek? dayOfWeek, int? dayOfMonth, TimeSpan deliveryTime, bool sendEmail, bool saveToDashboard, string? emailRecipients)
    {
        if (!Enum.IsDefined((LegacyReportFrequency)frequency))
            throw new BusinessRuleException("SpeedReading.ReportSchedule.Frequency.Invalid", "Geçersiz rapor sıklığı.");
        if (deliveryTime < TimeSpan.Zero || deliveryTime >= TimeSpan.FromDays(1))
            throw new BusinessRuleException("SpeedReading.ReportSchedule.DeliveryTime.Invalid", "Teslimat saati 00:00 ile 23:59:59 arasında olmalıdır.");
        var parsed = (LegacyReportFrequency)frequency;
        if (parsed == LegacyReportFrequency.Weekly && !dayOfWeek.HasValue)
            throw new BusinessRuleException("SpeedReading.ReportSchedule.DayOfWeek.Required", "Haftalık raporlar için haftanın günü gereklidir.");
        if (parsed == LegacyReportFrequency.Monthly && dayOfMonth is < 1 or > 31)
            throw new BusinessRuleException("SpeedReading.ReportSchedule.DayOfMonth.Invalid", "Aylık rapor günü 1-31 arasında olmalıdır.");
        return (parsed, parsed == LegacyReportFrequency.Weekly ? dayOfWeek : null, parsed == LegacyReportFrequency.Monthly ? dayOfMonth : null, deliveryTime, sendEmail, saveToDashboard, NormalizeRecipients(emailRecipients, sendEmail));
    }

    private static DateTime? CalculateNextRunAt(LegacyReportFrequency frequency, DayOfWeek? dayOfWeek, int? dayOfMonth, TimeSpan deliveryTime, DateTime nowUtc)
    {
        var zone = GetIstanbulTimeZone();
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), zone);
        DateTime nextLocal;
        var deliveryToday = localNow.Date.Add(deliveryTime);
        switch (frequency)
        {
            case LegacyReportFrequency.Daily:
                nextLocal = deliveryToday > localNow ? deliveryToday : deliveryToday.AddDays(1);
                break;
            case LegacyReportFrequency.Weekly:
                var daysUntil = ((int)dayOfWeek!.Value - (int)localNow.DayOfWeek + 7) % 7;
                if (daysUntil == 0 && deliveryToday <= localNow) daysUntil = 7;
                nextLocal = deliveryToday.AddDays(daysUntil);
                break;
            case LegacyReportFrequency.Monthly:
                var requestedDay = dayOfMonth!.Value;
                var currentDay = Math.Min(requestedDay, DateTime.DaysInMonth(localNow.Year, localNow.Month));
                nextLocal = new DateTime(localNow.Year, localNow.Month, currentDay).Add(deliveryTime);
                if (nextLocal <= localNow)
                {
                    var nextMonth = localNow.AddMonths(1);
                    nextLocal = new DateTime(nextMonth.Year, nextMonth.Month, Math.Min(requestedDay, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month))).Add(deliveryTime);
                }
                break;
            default:
                return null;
        }
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(nextLocal, DateTimeKind.Unspecified), zone);
    }

    private static TimeZoneInfo GetIstanbulTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
        catch (TimeZoneNotFoundException)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        }
    }

    private static string? NormalizeRecipients(string? value, bool sendEmail)
    {
        if (!sendEmail)
            return null;
        var recipients = (value ?? string.Empty).Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (recipients.Length == 0)
            throw new BusinessRuleException("SpeedReading.ReportSchedule.EmailRecipients.Required", "E-posta gönderimi için en az bir alıcı gereklidir.");
        foreach (var recipient in recipients)
        {
            try { _ = new MailAddress(recipient); }
            catch (FormatException) { throw new BusinessRuleException("SpeedReading.ReportSchedule.EmailRecipients.Invalid", "E-posta alıcılarından biri geçersiz."); }
        }
        return string.Join(',', recipients);
    }

    private static void ValidateSnapshotRequest(Guid actorId, string key, CreateReportSnapshotRequest request)
    {
        ValidateIdempotency(actorId, key);
        if (request is null || request.ReportTemplateId == Guid.Empty)
            throw new BusinessRuleException("SpeedReading.ReportSnapshot.Request.Invalid", "Geçerli bir rapor şablonu gereklidir.");
    }

    private static void ValidateIdempotency(Guid actorId, string key)
    {
        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(key) || key.Trim().Length is < 16 or > 128 || key.Trim().Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '_' or '-' or '~')))
            throw new BusinessRuleException("SpeedReading.IdempotencyKey.Invalid", "Geçerli bir Idempotency-Key gereklidir.");
    }

    private static string NormalizeKey(string key) => key.Trim();
    private static string CreateRequestHash(Guid actorId, string scope, Guid resourceId, object request) => SpeedReadingRequestHasher.Create(actorId.ToString("D"), scope, resourceId.ToString("D"), JsonSerializer.Serialize(request));
    private static void EnsureReplay(OwnedIdempotencyRecord record, string requestHash)
    {
        if (!record.Matches(requestHash))
            throw new BusinessRuleException("IdempotencyKey.Reused", "Idempotency-Key farklı bir istek için tekrar kullanılamaz.");
    }
    private static bool IsUniqueViolation(DbUpdateException exception) => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
