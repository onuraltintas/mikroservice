namespace SpeedReading.Application.Reports;

public sealed record ReportTemplateSummary(
    Guid Id,
    string Name,
    string Description,
    string Type,
    string Category,
    string ConfigurationJson,
    bool IsSystemTemplate,
    Guid? CreatedByUserId,
    DateTime CreatedAt,
    bool IsActive);

public sealed record ReportSnapshotSummary(
    Guid Id,
    Guid ReportTemplateId,
    string ReportTemplateName,
    DateTime GeneratedAt,
    DateTime ReportStartDate,
    DateTime ReportEndDate,
    string? PdfFileUrl,
    string? ExcelFileUrl,
    bool IsViewed,
    DateTime? ViewedAt);

public sealed record ReportSnapshotDetail(
    Guid Id,
    Guid GeneratedForUserId,
    Guid ReportTemplateId,
    string ReportTemplateName,
    DateTime GeneratedAt,
    DateTime ReportStartDate,
    DateTime ReportEndDate,
    string DataJson,
    bool DataJsonTruncated,
    string? PdfFileUrl,
    string? ExcelFileUrl,
    bool IsViewed,
    DateTime? ViewedAt);

public sealed record ScheduledReportSummary(
    Guid Id,
    Guid ReportTemplateId,
    string ReportTemplateName,
    string Frequency,
    DayOfWeek? DayOfWeek,
    int? DayOfMonth,
    TimeSpan DeliveryTime,
    bool IsActive,
    DateTime? LastRunAt,
    DateTime? NextRunAt,
    int SuccessCount,
    int FailureCount,
    bool SendEmail,
    bool SaveToDashboard,
    string? EmailRecipients);

public interface ILegacySpeedReadingReports
{
    Task<IReadOnlyList<ReportTemplateSummary>> GetTemplatesAsync(
        string? type,
        bool? isActive,
        Guid requestingUserId,
        bool isGlobalAdministrator,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ReportTemplateSummary?> GetTemplateAsync(
        Guid templateId,
        Guid requestingUserId,
        bool isGlobalAdministrator,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportSnapshotSummary>> GetUserSnapshotsAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ReportSnapshotDetail?> GetUserSnapshotAsync(
        Guid userId,
        Guid snapshotId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduledReportSummary>> GetUserScheduledReportsAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);
}
