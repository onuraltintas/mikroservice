namespace SpeedReading.Infrastructure.Legacy;

internal enum LegacyReportType
{
    Student,
    Teacher,
    Admin
}

internal enum LegacyReportCategory
{
    Dashboard,
    ReadingSpeed,
    Comprehension,
    Series,
    Activity,
    ClassOverview,
    StudentDetail,
    Assignment,
    CategoryAnalysis,
    TimeBasedProgress,
    Institution,
    PlatformUsage,
    ContentAnalysis,
    SystemHealth
}

internal enum LegacyReportFrequency
{
    Daily,
    Weekly,
    Monthly
}

internal sealed class LegacyReportTemplate : LegacyBaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public LegacyReportType Type { get; set; }
    public LegacyReportCategory Category { get; set; }
    public string ConfigurationJson { get; set; } = "{}";
    public bool IsSystemTemplate { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? CreatedById { get; set; }
    public bool IsActive { get; set; }
}

internal sealed class LegacyReportSnapshot : LegacyBaseEntity
{
    public Guid ReportTemplateId { get; set; }
    public Guid GeneratedForUserId { get; set; }
    public Guid? GeneratedByUserId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime ReportStartDate { get; set; }
    public DateTime ReportEndDate { get; set; }
    public string DataJson { get; set; } = "{}";
    public string? PdfFileUrl { get; set; }
    public string? ExcelFileUrl { get; set; }
    public bool IsViewed { get; set; }
    public DateTime? ViewedAt { get; set; }
}

internal sealed class LegacyScheduledReport : LegacyBaseEntity
{
    public Guid ReportTemplateId { get; set; }
    public Guid UserId { get; set; }
    public LegacyReportFrequency Frequency { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public TimeSpan DeliveryTime { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public bool SendEmail { get; set; }
    public bool SaveToDashboard { get; set; }
    public string? EmailRecipients { get; set; }
}
