using System.Net.Mail;
using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.Progress;
using SpeedReading.Application.Reports;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingReportsScheduleWriter(SpeedReadingDbContext db)
    : ISpeedReadingReportsScheduleWriter
{
    private const string CreateScope = "speed-reading.report-schedules.create";
    private const string UpdateScope = "speed-reading.report-schedules.update";
    private const string StatusScope = "speed-reading.report-schedules.status";
    private const string DeleteScope = "speed-reading.report-schedules.delete";

    public async Task<ScheduledReportSummary> CreateScheduledReportAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        CreateScheduledReportRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, idempotencyKey, request);
        var normalized = Normalize(request.Frequency, request.DayOfWeek, request.DayOfMonth,
            request.DeliveryTime, request.SendEmail, request.SaveToDashboard, request.EmailRecipients);
        await EnsureTemplateCanBeScheduledAsync(actorId, isGlobalAdministrator, request.ReportTemplateId, cancellationToken);

        var key = NormalizeKey(idempotencyKey);
        var hash = CreateRequestHash(actorId, CreateScope, Guid.Empty, request);
        var existing = await GetLedgerAsync(CreateScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return await GetSummaryAsync(existing.ResourceId, actorId, cancellationToken);
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
        AddLedger(CreateScope, key, hash, schedule.Id, now);
        var persistedId = await SaveOrReplayAsync(CreateScope, key, hash, schedule.Id, cancellationToken);
        return await GetSummaryAsync(persistedId, actorId, cancellationToken);
    }

    public async Task<ScheduledReportSummary> UpdateScheduledReportAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        Guid scheduleId,
        UpdateScheduledReportRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, idempotencyKey, request);
        var normalized = Normalize(request.Frequency, request.DayOfWeek, request.DayOfMonth,
            request.DeliveryTime, request.SendEmail, request.SaveToDashboard, request.EmailRecipients);
        var key = NormalizeKey(idempotencyKey);
        var hash = CreateRequestHash(actorId, UpdateScope, scheduleId, request);
        var existing = await GetLedgerAsync(UpdateScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return await GetSummaryAsync(existing.ResourceId, actorId, cancellationToken);
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

        AddLedger(UpdateScope, key, hash, schedule.Id, DateTime.UtcNow);
        var persistedId = await SaveOrReplayAsync(UpdateScope, key, hash, schedule.Id, cancellationToken);
        return await GetSummaryAsync(persistedId, actorId, cancellationToken);
    }

    public async Task<ScheduledReportSummary> UpdateScheduledReportStatusAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        Guid scheduleId,
        UpdateScheduledReportStatusRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, idempotencyKey, request);
        var key = NormalizeKey(idempotencyKey);
        var hash = CreateRequestHash(actorId, StatusScope, scheduleId, request);
        var existing = await GetLedgerAsync(StatusScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return await GetSummaryAsync(existing.ResourceId, actorId, cancellationToken);
        }

        var schedule = await GetOwnedScheduleAsync(actorId, scheduleId, cancellationToken);
        if (request.IsActive)
        {
            await EnsureTemplateCanBeScheduledAsync(actorId, isGlobalAdministrator, schedule.ReportTemplateId, cancellationToken);
        }

        schedule.IsActive = request.IsActive;
        schedule.NextRunAt = request.IsActive
            ? CalculateNextRunAt(schedule.Frequency, schedule.DayOfWeek, schedule.DayOfMonth, schedule.DeliveryTime, DateTime.UtcNow)
            : null;
        schedule.UpdatedAt = DateTime.UtcNow;
        schedule.UpdatedBy = actorId;

        AddLedger(StatusScope, key, hash, schedule.Id, DateTime.UtcNow);
        var persistedId = await SaveOrReplayAsync(StatusScope, key, hash, schedule.Id, cancellationToken);
        return await GetSummaryAsync(persistedId, actorId, cancellationToken);
    }

    public async Task DeleteScheduledReportAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        Guid scheduleId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = NormalizeKey(idempotencyKey);
        var hash = SpeedReadingRequestHasher.Create(actorId.ToString("D"), DeleteScope, scheduleId.ToString("D"));
        var existing = await GetLedgerAsync(DeleteScope, key, cancellationToken);
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

        AddLedger(DeleteScope, key, hash, schedule.Id, now);
        _ = await SaveOrReplayAsync(DeleteScope, key, hash, schedule.Id, cancellationToken);
    }

    private async Task EnsureTemplateCanBeScheduledAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var template = await db.ReportTemplates.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == templateId && !item.IsDeleted,
            cancellationToken)
            ?? throw new NotFoundException("ReportTemplate", templateId);

        if (!template.IsActive)
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportTemplate.Inactive",
                "Pasif rapor şablonu zamanlanamaz.");
        }

        if (!isGlobalAdministrator
            && (template.Type == LegacyReportType.Admin
                || (!template.IsSystemTemplate && template.CreatedByUserId != actorId)))
        {
            throw new BusinessRuleException(
                "Authorization.ReportTemplate.Scope",
                "Bu rapor şablonu üzerinde işlem yapma yetkiniz yok.");
        }
    }

    private async Task<LegacyScheduledReport> GetOwnedScheduleAsync(
        Guid actorId,
        Guid scheduleId,
        CancellationToken cancellationToken) =>
        await db.ScheduledReports.SingleOrDefaultAsync(
            item => item.Id == scheduleId && item.UserId == actorId && !item.IsDeleted,
            cancellationToken)
        ?? throw new NotFoundException("ScheduledReport", scheduleId);

    private async Task<ScheduledReportSummary> GetSummaryAsync(
        Guid scheduleId,
        Guid actorId,
        CancellationToken cancellationToken) =>
        await (
            from scheduled in db.ScheduledReports.AsNoTracking()
            join template in db.ReportTemplates.AsNoTracking()
                on scheduled.ReportTemplateId equals template.Id
            where scheduled.Id == scheduleId
                && scheduled.UserId == actorId
                && !scheduled.IsDeleted
            select new ScheduledReportSummary(
                scheduled.Id,
                scheduled.ReportTemplateId,
                template.Name,
                scheduled.Frequency.ToString(),
                scheduled.DayOfWeek,
                scheduled.DayOfMonth,
                scheduled.DeliveryTime,
                scheduled.IsActive,
                scheduled.LastRunAt,
                scheduled.NextRunAt,
                scheduled.SuccessCount,
                scheduled.FailureCount,
                scheduled.SendEmail,
                scheduled.SaveToDashboard,
                scheduled.EmailRecipients))
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new BusinessRuleException(
            "Idempotency.ResourceMissing",
            "Idempotency kaydına ait zamanlanmış rapor bulunamadı; yeni bir anahtar kullanın.");

    private async Task<LegacyIdempotencyRecord?> GetLedgerAsync(
        string scope,
        string key,
        CancellationToken cancellationToken) =>
        await db.IdempotencyRecords.SingleOrDefaultAsync(
            item => item.Scope == scope && item.Key == key,
            cancellationToken);

    private void AddLedger(string scope, string key, string requestHash, Guid resourceId, DateTime createdAt) =>
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

    private static (LegacyReportFrequency Frequency, DayOfWeek? DayOfWeek, int? DayOfMonth,
        TimeSpan DeliveryTime, bool SendEmail, bool SaveToDashboard, string? EmailRecipients) Normalize(
        int frequency,
        DayOfWeek? dayOfWeek,
        int? dayOfMonth,
        TimeSpan deliveryTime,
        bool sendEmail,
        bool saveToDashboard,
        string? emailRecipients)
    {
        if (!Enum.IsDefined((LegacyReportFrequency)frequency))
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportSchedule.Frequency.Invalid",
                "Geçersiz rapor sıklığı.");
        }

        if (deliveryTime < TimeSpan.Zero || deliveryTime >= TimeSpan.FromDays(1))
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportSchedule.DeliveryTime.Invalid",
                "Teslimat saati 00:00 ile 23:59:59 arasında olmalıdır.");
        }

        var parsedFrequency = (LegacyReportFrequency)frequency;
        if (parsedFrequency == LegacyReportFrequency.Weekly
            && (!dayOfWeek.HasValue || dayOfWeek.Value is < DayOfWeek.Sunday or > DayOfWeek.Saturday))
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportSchedule.DayOfWeek.Required",
                "Haftalık raporlar için haftanın günü gereklidir.");
        }

        if (parsedFrequency == LegacyReportFrequency.Monthly
            && (!dayOfMonth.HasValue || dayOfMonth is < 1 or > 31))
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportSchedule.DayOfMonth.Invalid",
                "Aylık rapor günü 1-31 arasında olmalıdır.");
        }

        var normalizedRecipients = NormalizeRecipients(emailRecipients, sendEmail);
        return (parsedFrequency,
            parsedFrequency == LegacyReportFrequency.Weekly ? dayOfWeek : null,
            parsedFrequency == LegacyReportFrequency.Monthly ? dayOfMonth : null,
            deliveryTime,
            sendEmail,
            saveToDashboard,
            normalizedRecipients);
    }

    private static DateTime? CalculateNextRunAt(
        LegacyReportFrequency frequency,
        DayOfWeek? dayOfWeek,
        int? dayOfMonth,
        TimeSpan deliveryTime,
        DateTime nowUtc)
    {
        var timeZone = GetIstanbulTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), timeZone);
        var deliveryToday = nowLocal.Date.Add(deliveryTime);
        DateTime nextLocal;

        switch (frequency)
        {
            case LegacyReportFrequency.Daily:
                nextLocal = deliveryToday > nowLocal ? deliveryToday : deliveryToday.AddDays(1);
                break;
            case LegacyReportFrequency.Weekly:
                var daysUntilTarget = ((int)dayOfWeek!.Value - (int)nowLocal.DayOfWeek + 7) % 7;
                if (daysUntilTarget == 0 && deliveryToday <= nowLocal)
                {
                    daysUntilTarget = 7;
                }
                nextLocal = deliveryToday.AddDays(daysUntilTarget);
                break;
            case LegacyReportFrequency.Monthly:
                var requestedDay = dayOfMonth!.Value;
                var currentMonthDay = Math.Min(requestedDay, DateTime.DaysInMonth(nowLocal.Year, nowLocal.Month));
                nextLocal = new DateTime(nowLocal.Year, nowLocal.Month, currentMonthDay).Add(deliveryTime);
                if (nextLocal <= nowLocal)
                {
                    var nextMonth = nowLocal.AddMonths(1);
                    var nextMonthDay = Math.Min(requestedDay, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                    nextLocal = new DateTime(nextMonth.Year, nextMonth.Month, nextMonthDay).Add(deliveryTime);
                }
                break;
            default:
                return null;
        }

        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(nextLocal, DateTimeKind.Unspecified), timeZone);
    }

    private static TimeZoneInfo GetIstanbulTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
        }
    }

    private static string? NormalizeRecipients(string? value, bool sendEmail)
    {
        if (!sendEmail || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var recipients = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (recipients.Length == 0 || recipients.Length > 50 || value.Length > 2_000 || value.Contains('\r') || value.Contains('\n'))
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportSchedule.EmailRecipients.Invalid",
                "E-posta alıcıları geçerli ve en fazla 50 adres olmalıdır.");
        }

        foreach (var recipient in recipients)
        {
            try
            {
                var parsed = new MailAddress(recipient);
                if (!string.Equals(parsed.Address, recipient, StringComparison.OrdinalIgnoreCase))
                {
                    throw new FormatException();
                }
            }
            catch (FormatException)
            {
                throw new BusinessRuleException(
                    "SpeedReading.ReportSchedule.EmailRecipients.Invalid",
                    "E-posta alıcılarından biri geçersiz.");
            }
        }

        return string.Join(',', recipients);
    }

    private static void ValidateRequest(Guid actorId, string idempotencyKey, CreateScheduledReportRequest? request)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (request is null || request.ReportTemplateId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportSchedule.Request.Invalid",
                "Rapor şablonu gereklidir.");
        }
    }

    private static void ValidateRequest(Guid actorId, string idempotencyKey, UpdateScheduledReportRequest? request)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (request is null)
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportSchedule.Request.Invalid",
                "Zamanlanmış rapor isteği gereklidir.");
        }
    }

    private static void ValidateRequest(Guid actorId, string idempotencyKey, UpdateScheduledReportStatusRequest? request)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (request is null)
        {
            throw new BusinessRuleException(
                "SpeedReading.ReportSchedule.Request.Invalid",
                "Durum isteği gereklidir.");
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

    private static string CreateRequestHash(Guid actorId, string scope, Guid resourceId, object request) =>
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
