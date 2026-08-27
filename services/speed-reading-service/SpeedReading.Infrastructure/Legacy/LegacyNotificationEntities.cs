namespace SpeedReading.Infrastructure.Legacy;

internal abstract class LegacyNotificationBase
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

internal sealed class LegacyUserNotification : LegacyNotificationBase
{
    public Guid UserId { get; set; }
    public int Type { get; set; }
    public int Channel { get; set; }
    public int Status { get; set; }
    public bool IsRead { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Data { get; set; }
    public string? ActionUrl { get; set; }
    public string? IconUrl { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public int Priority { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public string? UserRole { get; set; }
    public string? ErrorMessage { get; set; }
    public bool EmailSent { get; set; }
    public DateTime? EmailSentAt { get; set; }
    public bool PushSent { get; set; }
    public DateTime? PushSentAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
}

internal sealed class LegacyNotificationPreference : LegacyNotificationBase
{
    public Guid UserId { get; set; }
    public int NotificationType { get; set; }
    public bool EmailEnabled { get; set; }
    public bool PushEnabled { get; set; }
    public bool InAppEnabled { get; set; }
    public bool EnableInstant { get; set; }
    public bool EnableDaily { get; set; }
    public bool EnableWeekly { get; set; }
    public string? PreferredTime { get; set; }
    public bool SmsEnabled { get; set; }
    public bool AchievementsEnabled { get; set; }
    public bool LevelUpEnabled { get; set; }
    public bool DailyReminderEnabled { get; set; }
    public bool StreakMilestoneEnabled { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}

internal sealed class LegacyNotificationTypePreference : LegacyNotificationBase
{
    public Guid UserId { get; set; }
    public int NotificationType { get; set; }
    public bool EnableInApp { get; set; }
    public bool EnableEmail { get; set; }
    public bool EnablePush { get; set; }
    public string? PreferredTime { get; set; }
}

internal sealed class LegacyPushSubscription : LegacyNotificationBase
{
    public Guid UserId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256DH { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public bool IsActive { get; set; }
}

internal sealed class LegacyAnnouncement : LegacyNotificationBase
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "Banner";
    public int Priority { get; set; }
    public string TargetAudience { get; set; } = "All";
    public Guid? TargetInstitutionId { get; set; }
    public string? TargetRoles { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsPinned { get; set; }
    public bool IsActive { get; set; }
    public string? ActionUrl { get; set; }
    public string? ImageUrl { get; set; }
    public Guid CreatedByUserId { get; set; }

    // Additive fields used by the current client. EndDate/ImageUrl/Type stay
    // populated for older consumers.
    public string? PlainTextContent { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int DisplayType { get; set; }
    public string? Icon { get; set; }
    public string? ColorTheme { get; set; }
    public string? ActionText { get; set; }
    public bool SendEmailNotification { get; set; }
    public bool CreateInAppNotification { get; set; }
    public Guid? EmailCampaignId { get; set; }
}

internal sealed class LegacyAnnouncementUserInteraction : LegacyNotificationBase
{
    public Guid AnnouncementId { get; set; }
    public Guid UserId { get; set; }
    public DateTime? ViewedAt { get; set; }
    public DateTime? ClickedAt { get; set; }
    public DateTime? DismissedAt { get; set; }
}

internal sealed class LegacyEmailTemplate : LegacyNotificationBase
{
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Variables { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public string? Code { get; set; }
    public string? AvailableVariables { get; set; }
}

internal sealed class LegacyEmailCampaign : LegacyNotificationBase
{
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? TargetRoles { get; set; }
    public Guid? TargetInstitutionId { get; set; }
    public Guid? TemplateId { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }
    public string Status { get; set; } = "Draft";
    public int TotalRecipients { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string? PlainTextBody { get; set; }
    public bool IncludeAllUsers { get; set; }
    public bool IncludeSubscribers { get; set; }
    public int OpenedCount { get; set; }
    public int ClickedCount { get; set; }
}

internal sealed class LegacyEmailCampaignLog : LegacyNotificationBase
{
    public Guid CampaignId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
}

internal sealed class LegacyUserRoleLink
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}

internal sealed class LegacyRoleLookup
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
}
