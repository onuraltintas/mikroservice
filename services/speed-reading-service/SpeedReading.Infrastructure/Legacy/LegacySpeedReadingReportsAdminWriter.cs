using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.Progress;
using SpeedReading.Application.Reports;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingReportsAdminWriter(SpeedReadingDbContext db)
    : ISpeedReadingReportsAdminWriter
{
    private const string CreateScope = "speed-reading.report-templates.create";
    private const string UpdateScope = "speed-reading.report-templates.update";
    private const string DeleteScope = "speed-reading.report-templates.delete";
    private const int MaxConfigurationLength = 1_000_000;

    public async Task<ReportTemplateSummary> CreateTemplateAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        CreateReportTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, idempotencyKey, request);
        var type = ParseType(request.Type);
        var category = ParseCategory(request.Category);
        EnsureCategoryScope(type, category, isGlobalAdministrator);

        var key = NormalizeKey(idempotencyKey);
        var hash = CreateRequestHash(actorId, CreateScope, Guid.Empty, request);
        var existing = await GetLedgerAsync(CreateScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return await GetSummaryAsync(existing.ResourceId, actorId, isGlobalAdministrator, cancellationToken);
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
            IsActive = true
        };

        db.ReportTemplates.Add(template);
        AddLedger(CreateScope, key, hash, template.Id, now);
        var persistedId = await SaveOrReplayAsync(
            CreateScope, key, hash, template.Id, cancellationToken);
        return await GetSummaryAsync(persistedId, actorId, isGlobalAdministrator, cancellationToken);
    }

    public async Task<ReportTemplateSummary> UpdateTemplateAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        Guid templateId,
        UpdateReportTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, idempotencyKey, request);
        var key = NormalizeKey(idempotencyKey);
        var hash = CreateRequestHash(actorId, UpdateScope, templateId, request);
        var existing = await GetLedgerAsync(UpdateScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return await GetSummaryAsync(existing.ResourceId, actorId, isGlobalAdministrator, cancellationToken);
        }

        var template = await GetTemplateForWriteAsync(
            actorId,
            isGlobalAdministrator,
            templateId,
            cancellationToken);

        template.Name = request.Name.Trim();
        template.Description = request.Description.Trim();
        template.ConfigurationJson = request.ConfigurationJson.Trim();
        template.IsActive = request.IsActive;
        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedBy = actorId;

        AddLedger(UpdateScope, key, hash, template.Id, DateTime.UtcNow);
        var persistedId = await SaveOrReplayAsync(
            UpdateScope, key, hash, template.Id, cancellationToken);
        return await GetSummaryAsync(persistedId, actorId, isGlobalAdministrator, cancellationToken);
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
        var hash = SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), DeleteScope, templateId.ToString("D"));
        var existing = await GetLedgerAsync(DeleteScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return;
        }

        var template = await GetTemplateForWriteAsync(
            actorId,
            isGlobalAdministrator,
            templateId,
            cancellationToken);

        if (await db.ScheduledReports.AnyAsync(
                item => item.ReportTemplateId == templateId
                    && item.IsActive
                    && !item.IsDeleted,
                cancellationToken))
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportTemplate.HasActiveSchedules",
                "Aktif zamanlanmış raporları olan şablon silinemez.");
        }

        var now = DateTime.UtcNow;
        template.IsDeleted = true;
        template.IsActive = false;
        template.DeletedAt = now;
        template.DeletedBy = actorId;
        template.UpdatedAt = now;
        template.UpdatedBy = actorId;
        AddLedger(DeleteScope, key, hash, template.Id, now);
        _ = await SaveOrReplayAsync(DeleteScope, key, hash, template.Id, cancellationToken);
    }

    private async Task<LegacyReportTemplate> GetTemplateForWriteAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var template = await db.ReportTemplates.SingleOrDefaultAsync(
            item => item.Id == templateId && !item.IsDeleted,
            cancellationToken)
            ?? throw new NotFoundException("ReportTemplate", templateId);

        if (!isGlobalAdministrator
            && (template.Type == LegacyReportType.Admin
                || (!template.IsSystemTemplate && template.CreatedByUserId != actorId)))
        {
            throw new BusinessRuleException(
                "Authorization.ReportTemplate.Scope",
                "Bu rapor şablonu üzerinde işlem yapma yetkiniz yok.");
        }

        if (template.IsSystemTemplate && !isGlobalAdministrator)
        {
            throw new BusinessRuleException(
                "Authorization.ReportTemplate.SystemTemplate",
                "Sistem rapor şablonları yalnızca SystemAdmin tarafından değiştirilebilir.");
        }

        return template;
    }

    private async Task<ReportTemplateSummary> GetSummaryAsync(
        Guid templateId,
        Guid actorId,
        bool isGlobalAdministrator,
        CancellationToken cancellationToken)
    {
        var template = await db.ReportTemplates.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == templateId
                && !item.IsDeleted
                && (isGlobalAdministrator
                    || (item.Type != LegacyReportType.Admin
                        && (item.IsSystemTemplate || item.CreatedByUserId == actorId))),
            cancellationToken)
            ?? throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait rapor şablonu bulunamadı; yeni bir anahtar kullanın.");
        return ToSummary(template);
    }

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

    private static ReportTemplateSummary ToSummary(LegacyReportTemplate item) => new(
        item.Id,
        item.Name,
        item.Description,
        item.Type.ToString(),
        item.Category.ToString(),
        item.ConfigurationJson,
        item.IsSystemTemplate,
        item.CreatedByUserId,
        item.CreatedAt,
        item.IsActive);

    private static LegacyReportType ParseType(int value) =>
        Enum.IsDefined((LegacyReportType)value)
            ? (LegacyReportType)value
            : throw new BusinessRuleException(
                "SpeedReading.ReportTemplate.Type.Invalid",
                "Geçersiz rapor şablonu türü.");

    private static LegacyReportCategory ParseCategory(int value) =>
        Enum.IsDefined((LegacyReportCategory)value)
            ? (LegacyReportCategory)value
            : throw new BusinessRuleException(
                "SpeedReading.ReportTemplate.Category.Invalid",
                "Geçersiz rapor şablonu kategorisi.");

    private static void EnsureCategoryScope(
        LegacyReportType type,
        LegacyReportCategory category,
        bool isGlobalAdministrator)
    {
        if (isGlobalAdministrator)
        {
            return;
        }

        if (type == LegacyReportType.Admin
            || category is LegacyReportCategory.Institution
                or LegacyReportCategory.PlatformUsage
                or LegacyReportCategory.ContentAnalysis
                or LegacyReportCategory.SystemHealth)
        {
            throw new BusinessRuleException(
                "Authorization.ReportTemplate.AdminScope",
                "Yönetim rapor şablonları yalnızca SystemAdmin tarafından yönetilebilir.");
        }
    }

    private static void ValidateRequest(
        Guid actorId,
        string idempotencyKey,
        CreateReportTemplateRequest request)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (request is null)
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportTemplate.Request.Invalid",
                "Rapor şablonu isteği gereklidir.");
        }
        ValidateFields(request.Name, request.Description, request.ConfigurationJson);
    }

    private static void ValidateRequest(
        Guid actorId,
        string idempotencyKey,
        UpdateReportTemplateRequest request)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (request is null)
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportTemplate.Request.Invalid",
                "Rapor şablonu isteği gereklidir.");
        }
        ValidateFields(request.Name, request.Description, request.ConfigurationJson);
    }

    private static void ValidateFields(string? name, string? description, string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportTemplate.Name.Invalid",
                "Rapor şablonu adı 1-200 karakter olmalıdır.");
        }

        if (description?.Trim().Length > 1_000)
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportTemplate.Description.Invalid",
                "Rapor şablonu açıklaması en fazla 1000 karakter olabilir.");
        }

        if (string.IsNullOrWhiteSpace(configurationJson)
            || configurationJson?.Length > MaxConfigurationLength
            || !IsJsonObjectOrArray(configurationJson))
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportTemplate.Configuration.Invalid",
                "Rapor şablonu yapılandırması geçerli bir JSON nesnesi veya dizisi olmalıdır.");
        }
    }

    private static bool IsJsonObjectOrArray(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

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

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
