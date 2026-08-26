namespace SpeedReading.Application.Notifications;

public sealed record NotificationSummary(
    Guid Id,
    Guid UserId,
    string Title,
    string Message,
    int Type,
    string TypeName,
    int Priority,
    string PriorityName,
    int Status,
    string? ActionUrl,
    string? IconUrl,
    DateTime CreatedAt,
    DateTime? ReadAt)
{
    public bool IsRead => ReadAt.HasValue;
}

public sealed record AdminNotificationSummary(
    Guid Id,
    Guid UserId,
    string Title,
    string Message,
    int Type,
    string TypeName,
    int Priority,
    string PriorityName,
    int Status,
    string? ActionUrl,
    string? IconUrl,
    DateTime CreatedAt,
    DateTime? ReadAt,
    string UserName,
    string UserEmail,
    string UserRole)
{
    public bool IsRead => ReadAt.HasValue;
}

public sealed record NotificationPage(
    IReadOnlyList<NotificationSummary> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record AdminNotificationPage(
    IReadOnlyList<AdminNotificationSummary> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record UnreadNotificationCount(int Total, int High, int Urgent);

public sealed record NotificationPreferenceSummary(
    Guid Id,
    int NotificationType,
    bool EnableInApp,
    bool EnableEmail,
    bool EnablePush,
    string? PreferredTime);

public sealed record SubscribePushRequest(string Endpoint, string? P256DH, string? Auth);

public sealed record CreateNotificationRequest(
    Guid UserId,
    string Title,
    string Message,
    int Type = 9,
    int Priority = 2,
    string? ActionUrl = null,
    string? IconUrl = null);

public sealed record BulkNotificationRequest(
    string TargetType,
    string? TargetRole,
    string Title,
    string Message,
    int Type = 9,
    int Priority = 2,
    string? ActionUrl = null,
    bool SendEmail = false);

public sealed record BulkNotificationResult(
    bool Success,
    int TotalSent,
    int TotalFailed,
    int EmailsSent,
    IReadOnlyList<string> Errors);

public sealed record AnnouncementSummary(
    Guid Id,
    string Title,
    string Content,
    string? PlainTextContent,
    int Priority,
    int DisplayType,
    string? Icon,
    string? ColorTheme,
    string? ActionUrl,
    string? ActionText,
    bool IsPinned,
    DateTime? StartDate,
    DateTime? ExpiresAt,
    DateTime CreatedAt,
    bool HasViewed,
    bool HasDismissed,
    bool HasClicked);

public sealed record AnnouncementDetail(
    Guid Id,
    string Title,
    string Content,
    string? PlainTextContent,
    int Priority,
    int DisplayType,
    string? Icon,
    string? ColorTheme,
    string? ActionUrl,
    string? ActionText,
    bool IsPinned,
    DateTime? StartDate,
    DateTime? ExpiresAt,
    DateTime CreatedAt,
    bool HasViewed,
    bool HasDismissed,
    bool HasClicked,
    int TargetAudience,
    Guid? TargetInstitutionId,
    IReadOnlyList<string> TargetRoles,
    bool IsActive,
    bool SendEmailNotification,
    bool CreateInAppNotification,
    Guid? EmailCampaignId,
    int ViewCount,
    int ClickCount,
    DateTime? UpdatedAt,
    Guid CreatedBy);

public sealed record AnnouncementStats(
    Guid AnnouncementId,
    int TotalViewCount,
    int UniqueViewCount,
    int TotalClickCount,
    int UniqueClickCount,
    int DismissCount,
    decimal ViewRate,
    decimal ClickThroughRate,
    decimal DismissRate,
    DateTime? FirstViewedAt,
    DateTime? LastViewedAt);

public sealed record CreateAnnouncementRequest(
    string Title,
    string Content,
    string? PlainTextContent,
    int Priority,
    int TargetAudience,
    Guid? TargetInstitutionId,
    IReadOnlyList<string>? TargetRoles,
    bool IsPinned,
    DateTime? StartDate,
    DateTime? ExpiresAt,
    int DisplayType,
    string? Icon,
    string? ColorTheme,
    string? ActionUrl,
    string? ActionText,
    bool SendEmailNotification,
    bool CreateInAppNotification);

public sealed record UpdateAnnouncementRequest(
    string Title,
    string Content,
    string? PlainTextContent,
    int Priority,
    int TargetAudience,
    Guid? TargetInstitutionId,
    IReadOnlyList<string>? TargetRoles,
    bool IsPinned,
    bool IsActive,
    DateTime? StartDate,
    DateTime? ExpiresAt,
    int DisplayType,
    string? Icon,
    string? ColorTheme,
    string? ActionUrl,
    string? ActionText);

public sealed record EmailTemplateSummary(
    Guid Id,
    string Name,
    string Code,
    string Subject,
    string Body,
    string Description,
    string AvailableVariables,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastModifiedAt);

public sealed record CreateEmailTemplateRequest(
    string Name,
    string Code,
    string Subject,
    string Body,
    string? Description,
    string? AvailableVariables,
    bool IsActive = true);

public sealed record UpdateEmailTemplateRequest(
    string Name,
    string Code,
    string Subject,
    string Body,
    string? Description,
    string? AvailableVariables,
    bool IsActive = true);

public sealed record EmailTemplatePreview(string Subject, string Body);

public sealed record EmailCampaignSummary(
    Guid Id,
    string Name,
    string Subject,
    int Status,
    string? TargetRoles,
    Guid? TargetInstitutionId,
    bool IncludeAllUsers,
    bool IncludeSubscribers,
    DateTime? ScheduledFor,
    DateTime? SentAt,
    int TotalRecipients,
    int SentCount,
    int FailedCount,
    int OpenedCount,
    int ClickedCount,
    DateTime CreatedAt);

public sealed record EmailCampaignDetail(
    EmailCampaignSummary Campaign,
    string Body,
    string? PlainTextBody,
    IReadOnlyList<EmailCampaignLogSummary> Logs);

public sealed record EmailCampaignLogSummary(
    Guid Id,
    string RecipientEmail,
    string Status,
    DateTime? SentAt,
    string? ErrorMessage);

public sealed record CreateEmailCampaignRequest(
    string Name,
    string Subject,
    string Body,
    string? PlainTextBody,
    string? TargetRoles,
    Guid? TargetInstitutionId,
    bool IncludeAllUsers,
    bool IncludeSubscribers,
    DateTime? ScheduledFor);

public sealed record UpdateEmailCampaignRequest(
    string Name,
    string Subject,
    string Body,
    string? PlainTextBody,
    string? TargetRoles,
    Guid? TargetInstitutionId,
    bool IncludeAllUsers,
    bool IncludeSubscribers,
    DateTime? ScheduledFor);

public sealed record SendEmailCampaignRequest(bool SendNow);

public sealed record EmailCampaignStats(
    int TotalRecipients,
    int SentCount,
    int FailedCount,
    int OpenedCount,
    int ClickedCount,
    int PendingCount);

public interface ISpeedReadingNotifications
{
    Task<NotificationPage> GetNotificationsAsync(Guid userId, bool? isRead, int? type, DateTime? fromDate, DateTime? toDate, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<UnreadNotificationCount> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationPreferenceSummary>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken);
    Task UpdatePreferencesAsync(Guid userId, IReadOnlyList<NotificationPreferenceSummary> preferences, CancellationToken cancellationToken);
    Task<Guid> SubscribePushAsync(Guid userId, SubscribePushRequest request, string? userAgent, CancellationToken cancellationToken);
    Task<bool> MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken);
    Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken);
    Task<NotificationSummary> CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken);
    Task<AdminNotificationPage> GetAllAsync(Guid? userId, int? type, bool? isRead, string? userRole, DateTime? fromDate, DateTime? toDate, string? searchTerm, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<BulkNotificationResult> SendBulkAsync(BulkNotificationRequest request, CancellationToken cancellationToken);
}

public interface ISpeedReadingAnnouncements
{
    Task<IReadOnlyList<AnnouncementSummary>> GetMyAsync(Guid userId, IReadOnlyCollection<string> roles, Guid? institutionId, bool includeDismissed, bool onlyPinned, CancellationToken cancellationToken);
    Task<IReadOnlyList<AnnouncementDetail>> GetAllAsync(bool? isActive, bool? isPinned, int? targetAudience, Guid? targetInstitutionId, bool includeExpired, int? take, Guid userId, CancellationToken cancellationToken);
    Task<AnnouncementStats?> GetStatsAsync(Guid id, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(Guid userId, CreateAnnouncementRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Guid id, UpdateAnnouncementRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> RecordViewAsync(Guid userId, Guid id, CancellationToken cancellationToken);
    Task<bool> RecordClickAsync(Guid userId, Guid id, CancellationToken cancellationToken);
    Task<bool> DismissAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}

public interface ISpeedReadingEmailTemplates
{
    Task<IReadOnlyList<EmailTemplateSummary>> GetAllAsync(CancellationToken cancellationToken);
    Task<EmailTemplateSummary?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<EmailTemplateSummary> CreateAsync(CreateEmailTemplateRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Guid id, UpdateEmailTemplateRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<EmailTemplatePreview?> PreviewAsync(Guid id, IReadOnlyDictionary<string, string>? variables, CancellationToken cancellationToken);
}

public interface ISpeedReadingEmailCampaigns
{
    Task<IReadOnlyList<EmailCampaignSummary>> GetAllAsync(int? status, CancellationToken cancellationToken);
    Task<EmailCampaignDetail?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<EmailCampaignSummary> CreateAsync(Guid userId, CreateEmailCampaignRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Guid id, UpdateEmailCampaignRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<EmailCampaignSummary?> SendAsync(Guid id, SendEmailCampaignRequest request, CancellationToken cancellationToken);
    Task<EmailCampaignStats?> GetStatsAsync(Guid id, CancellationToken cancellationToken);
}
