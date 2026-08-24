using Microsoft.EntityFrameworkCore;
using EduPlatform.Shared.Kernel.Exceptions;
using SpeedReading.Application.Reports;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingReports(SpeedReadingDbContext db)
    : ILegacySpeedReadingReports
{
    private const int MaxDataJsonLength = 1_000_000;

    public async Task<IReadOnlyList<ReportTemplateSummary>> GetTemplatesAsync(
        string? type,
        bool? isActive,
        Guid requestingUserId,
        bool isGlobalAdministrator,
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var query = db.ReportTemplates
            .AsNoTracking()
            .Where(item => !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<LegacyReportType>(type, true, out var reportType))
            {
                throw new BusinessRuleException(
                    "SpeedReading.Reports.TemplateType.Invalid",
                    "Geçersiz rapor şablonu türü.");
            }

            query = query.Where(item => item.Type == reportType);
        }

        if (isActive.HasValue)
        {
            query = query.Where(item => item.IsActive == isActive.Value);
        }

        if (!isGlobalAdministrator)
        {
            query = query.Where(item => item.Type != LegacyReportType.Admin
                && (item.IsSystemTemplate || item.CreatedByUserId == requestingUserId));
        }

        return await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.Name)
            .Select(item => new ReportTemplateSummary(
                item.Id,
                item.Name,
                item.Description,
                item.Type.ToString(),
                item.Category.ToString(),
                item.ConfigurationJson,
                item.IsSystemTemplate,
                item.CreatedByUserId,
                item.CreatedAt,
                item.IsActive))
            .Take(limit)
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
            .Select(item => new ReportTemplateSummary(
                item.Id,
                item.Name,
                item.Description,
                item.Type.ToString(),
                item.Category.ToString(),
                item.ConfigurationJson,
                item.IsSystemTemplate,
                item.CreatedByUserId,
                item.CreatedAt,
                item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ReportSnapshotSummary>> GetUserSnapshotsAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        return await (
            from snapshot in db.ReportSnapshots.AsNoTracking()
            join template in db.ReportTemplates.AsNoTracking()
                on snapshot.ReportTemplateId equals template.Id
            where snapshot.GeneratedForUserId == userId
                && !snapshot.IsDeleted
                && !template.IsDeleted
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
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<ReportSnapshotDetail?> GetUserSnapshotAsync(
        Guid userId,
        Guid snapshotId,
        CancellationToken cancellationToken = default) =>
        await (
            from snapshot in db.ReportSnapshots.AsNoTracking()
            join template in db.ReportTemplates.AsNoTracking()
                on snapshot.ReportTemplateId equals template.Id
            where snapshot.Id == snapshotId
                && snapshot.GeneratedForUserId == userId
                && !snapshot.IsDeleted
                && !template.IsDeleted
            select new ReportSnapshotDetail(
                snapshot.Id,
                snapshot.GeneratedForUserId,
                snapshot.ReportTemplateId,
                template.Name,
                snapshot.GeneratedAt,
                snapshot.ReportStartDate,
                snapshot.ReportEndDate,
                snapshot.DataJson.Length > MaxDataJsonLength
                    ? string.Empty
                    : snapshot.DataJson,
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
            from scheduled in db.ScheduledReports.AsNoTracking()
            join template in db.ReportTemplates.AsNoTracking()
                on scheduled.ReportTemplateId equals template.Id
            where scheduled.UserId == userId
                && !scheduled.IsDeleted
                && !template.IsDeleted
            orderby scheduled.IsActive descending, scheduled.NextRunAt
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
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);
}
