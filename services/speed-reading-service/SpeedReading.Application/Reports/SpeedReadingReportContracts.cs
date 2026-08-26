using System.Globalization;
using System.Text.Json;

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

public sealed record CreateReportTemplateRequest(
    string Name,
    string Description,
    int Type,
    int Category,
    string ConfigurationJson);

public sealed record UpdateReportTemplateRequest(
    string Name,
    string Description,
    string ConfigurationJson,
    bool IsActive);

public sealed record CreateScheduledReportRequest(
    Guid ReportTemplateId,
    int Frequency,
    DayOfWeek? DayOfWeek,
    int? DayOfMonth,
    TimeSpan DeliveryTime,
    bool SendEmail,
    bool SaveToDashboard,
    string? EmailRecipients);

public sealed record UpdateScheduledReportRequest(
    int Frequency,
    DayOfWeek? DayOfWeek,
    int? DayOfMonth,
    TimeSpan DeliveryTime,
    bool IsActive,
    bool SendEmail,
    bool SaveToDashboard,
    string? EmailRecipients);

public sealed record UpdateScheduledReportStatusRequest(bool IsActive);

public sealed record CreateReportSnapshotRequest(
    Guid ReportTemplateId,
    DateTime? ReportStartDate,
    DateTime? ReportEndDate,
    JsonElement? Data);

public sealed record ReportExportRequest(
    string? ReportType,
    string? Title,
    DateTime? StartDate,
    DateTime? EndDate,
    JsonElement? Data);

public sealed record ReportExportRow(string Field, string Value);

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

    Task<ScheduledReportSummary?> GetUserScheduledReportAsync(
        Guid userId,
        Guid scheduleId,
        CancellationToken cancellationToken = default);
}

public interface ISpeedReadingReportsAdminWriter
{
    Task<ReportTemplateSummary> CreateTemplateAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        CreateReportTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ReportTemplateSummary> UpdateTemplateAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        Guid templateId,
        UpdateReportTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task DeleteTemplateAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        Guid templateId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

public interface ISpeedReadingReportsScheduleWriter
{
    Task<ScheduledReportSummary> CreateScheduledReportAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        CreateScheduledReportRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ScheduledReportSummary> UpdateScheduledReportAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        Guid scheduleId,
        UpdateScheduledReportRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ScheduledReportSummary> UpdateScheduledReportStatusAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        Guid scheduleId,
        UpdateScheduledReportStatusRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task DeleteScheduledReportAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        Guid scheduleId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

public interface ISpeedReadingReportsSnapshotWriter
{
    Task<ReportSnapshotDetail> CreateSnapshotAsync(
        Guid actorId,
        bool isGlobalAdministrator,
        CreateReportSnapshotRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task DeleteSnapshotAsync(
        Guid actorId,
        Guid snapshotId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

public interface ISpeedReadingReportExporter
{
    byte[] GeneratePdf(ReportExportRequest request);

    byte[] GenerateExcel(ReportExportRequest request);
}

public static class SpeedReadingReportExportRules
{
    public const int MaxRows = 1_000;

    public static ReportExportRequest Normalize(JsonElement? payload)
    {
        if (!payload.HasValue || payload.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return new ReportExportRequest(null, null, null, null, null);
        }

        var value = payload.Value;
        if (value.GetRawText().Length > SpeedReadingReportSnapshotRules.MaxDataJsonLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payload),
                $"Rapor verisi {SpeedReadingReportSnapshotRules.MaxDataJsonLength} karakteri aşamaz.");
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return new ReportExportRequest(null, null, null, null, value);
        }

        var data = TryGetProperty(value, "data", out var nestedData) ? nestedData : value;
        return new ReportExportRequest(
            GetString(value, "reportType"),
            GetString(value, "title"),
            GetDate(value, "startDate"),
            GetDate(value, "endDate"),
            data);
    }

    public static string ResolveTitle(ReportExportRequest request) =>
        string.IsNullOrWhiteSpace(request.Title)
            ? string.IsNullOrWhiteSpace(request.ReportType)
                ? "Hızlı Okuma Raporu"
                : $"Hızlı Okuma Raporu - {request.ReportType}"
            : request.Title.Trim();

    public static IReadOnlyList<ReportExportRow> Flatten(JsonElement? data)
    {
        if (!data.HasValue || data.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return Array.Empty<ReportExportRow>();
        }

        var rows = new List<ReportExportRow>();
        FlattenValue(data.Value, string.Empty, rows, 0);
        return rows;
    }

    private static void FlattenValue(JsonElement value, string path, List<ReportExportRow> rows, int depth)
    {
        if (rows.Count >= MaxRows)
        {
            throw new ArgumentOutOfRangeException(nameof(rows), $"Rapor en fazla {MaxRows} alan içerebilir.");
        }

        if (depth > 8)
        {
            rows.Add(new ReportExportRow(path, value.GetRawText()));
            return;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    FlattenValue(property.Value, JoinPath(path, property.Name), rows, depth + 1);
                }

                if (path.Length > 0 && value.EnumerateObject().Count() == 0)
                {
                    rows.Add(new ReportExportRow(path, "{}"));
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    FlattenValue(item, $"{path}[{index}]", rows, depth + 1);
                    index++;
                }

                if (path.Length > 0 && index == 0)
                {
                    rows.Add(new ReportExportRow(path, "[]"));
                }

                break;
            case JsonValueKind.String:
                rows.Add(new ReportExportRow(path.Length == 0 ? "value" : path, value.GetString() ?? string.Empty));
                break;
            case JsonValueKind.Null:
                rows.Add(new ReportExportRow(path.Length == 0 ? "value" : path, ""));
                break;
            default:
                rows.Add(new ReportExportRow(path.Length == 0 ? "value" : path, value.GetRawText()));
                break;
        }
    }

    private static string JoinPath(string path, string propertyName) =>
        path.Length == 0 ? propertyName : $"{path}.{propertyName}";

    private static string? GetString(JsonElement value, string propertyName) =>
        TryGetProperty(value, propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool TryGetProperty(JsonElement value, string propertyName, out JsonElement property)
    {
        if (value.TryGetProperty(propertyName, out property))
        {
            return true;
        }

        foreach (var candidate in value.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private static DateTime? GetDate(JsonElement value, string propertyName)
    {
        var text = GetString(value, propertyName);
        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed.UtcDateTime
            : null;
    }
}

public static class SpeedReadingReportSnapshotRules
{
    public const int MaxDataJsonLength = 1_000_000;

    public static string NormalizeDataJson(JsonElement? data)
    {
        if (!data.HasValue || data.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "{}";
        }

        var json = data.Value.GetRawText();
        if (json.Length > MaxDataJsonLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                $"Snapshot verisi {MaxDataJsonLength} karakteri aşamaz.");
        }

        return json;
    }

    public static (DateTime Start, DateTime End) ResolveDateRange(
        DateTime? start,
        DateTime? end,
        DateTime utcNow)
    {
        var resolvedEnd = end ?? utcNow;
        var resolvedStart = start ?? resolvedEnd.AddDays(-30);
        if (resolvedStart > resolvedEnd)
        {
            throw new ArgumentException("Rapor başlangıç tarihi bitiş tarihinden sonra olamaz.");
        }

        if (resolvedEnd - resolvedStart > TimeSpan.FromDays(366))
        {
            throw new ArgumentOutOfRangeException(
                nameof(start),
                "Snapshot tarih aralığı 366 günden uzun olamaz.");
        }

        return (resolvedStart, resolvedEnd);
    }
}
