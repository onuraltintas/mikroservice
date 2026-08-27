using Microsoft.EntityFrameworkCore;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingNotificationBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedNotificationBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var sourceNotifications = await legacy.Notifications.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var notification in sourceNotifications)
        {
            notification.Channel = 0;
            notification.Status = notification.IsRead ? 4 : 0;
            notification.SentAt = notification.EmailSentAt ?? notification.PushSentAt;
        }
        var sourcePreferences = await legacy.NotificationPreferences.AsNoTracking().ToListAsync(cancellationToken);
        // The legacy database stores type preferences in NotificationPreferences.
        // The separate NotificationTypePreferences table exists only in the
        // compatibility model, so materialize the owned type rows from the same
        // source records instead of querying a table that is not present.
        var sourceTypePreferences = sourcePreferences
            .Select(preference => new LegacyNotificationTypePreference
            {
                Id = preference.Id,
                UserId = preference.UserId,
                NotificationType = preference.NotificationType,
                EnableInApp = preference.InAppEnabled,
                EnableEmail = preference.EmailEnabled,
                EnablePush = preference.PushEnabled,
                PreferredTime = preference.PreferredTime,
                CreatedAt = preference.CreatedAt,
                CreatedBy = preference.CreatedBy,
                UpdatedAt = preference.UpdatedAt,
                UpdatedBy = preference.UpdatedBy,
                IsDeleted = preference.IsDeleted,
                DeletedAt = preference.DeletedAt,
                DeletedBy = preference.DeletedBy
            })
            .ToList();
        var sourcePushSubscriptions = await legacy.PushSubscriptions.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var subscription in sourcePushSubscriptions)
            subscription.IsActive = !subscription.IsDeleted;
        var sourceAnnouncements = await legacy.Announcements.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var announcement in sourceAnnouncements)
        {
            announcement.Type = "Banner";
            announcement.EndDate = announcement.ExpiresAt;
            announcement.CreatedByUserId = announcement.CreatedBy;
        }
        var sourceInteractions = await legacy.AnnouncementUserInteractions.AsNoTracking().ToListAsync(cancellationToken);
        var sourceTemplates = await legacy.EmailTemplates.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var template in sourceTemplates)
            template.Variables = template.AvailableVariables;
        var sourceCampaigns = await legacy.EmailCampaigns.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var campaign in sourceCampaigns)
            campaign.CreatedByUserId = campaign.CreatedBy;
        var sourceCampaignLogs = await legacy.EmailCampaignLogs.AsNoTracking().ToListAsync(cancellationToken);

        var announcementIds = sourceAnnouncements.Select(item => item.Id).ToHashSet();
        var campaignIds = sourceCampaigns.Select(item => item.Id).ToHashSet();
        if (sourceInteractions.Any(item => !announcementIds.Contains(item.AnnouncementId)))
            throw new InvalidOperationException("Announcement interaction references a missing announcement.");
        if (sourceCampaignLogs.Any(item => !campaignIds.Contains(item.CampaignId)))
            throw new InvalidOperationException("Email campaign log references a missing campaign.");

        var target = (ISpeedReadingDataContext)owned;
        var targetNotifications = await target.Notifications.ToDictionaryAsync(item => item.Id, cancellationToken);
        var targetPreferences = await target.NotificationPreferences.ToDictionaryAsync(item => item.Id, cancellationToken);
        var targetTypePreferences = await target.NotificationTypePreferences.ToDictionaryAsync(item => item.Id, cancellationToken);
        var targetPushSubscriptions = await target.PushSubscriptions.ToDictionaryAsync(item => item.Id, cancellationToken);
        var targetAnnouncements = await target.Announcements.ToDictionaryAsync(item => item.Id, cancellationToken);
        var targetInteractions = await target.AnnouncementUserInteractions.ToDictionaryAsync(item => item.Id, cancellationToken);
        var targetTemplates = await target.EmailTemplates.ToDictionaryAsync(item => item.Id, cancellationToken);
        var targetCampaigns = await target.EmailCampaigns.ToDictionaryAsync(item => item.Id, cancellationToken);
        var targetCampaignLogs = await target.EmailCampaignLogs.ToDictionaryAsync(item => item.Id, cancellationToken);

        SyncAudit(sourceNotifications, targetNotifications);
        SyncAudit(sourcePreferences, targetPreferences);
        SyncAudit(sourceTypePreferences, targetTypePreferences);
        SyncAudit(sourcePushSubscriptions, targetPushSubscriptions);
        SyncAudit(sourceAnnouncements, targetAnnouncements);
        SyncAudit(sourceInteractions, targetInteractions);
        SyncAudit(sourceTemplates, targetTemplates);
        SyncAudit(sourceCampaigns, targetCampaigns);
        SyncAudit(sourceCampaignLogs, targetCampaignLogs);

        var notifications = ImportMissing(sourceNotifications, target.Notifications, targetNotifications.Keys.ToHashSet());
        var preferences = ImportMissing(sourcePreferences, target.NotificationPreferences, targetPreferences.Keys.ToHashSet());
        var typePreferences = ImportMissing(sourceTypePreferences, target.NotificationTypePreferences, targetTypePreferences.Keys.ToHashSet());
        var pushSubscriptions = ImportMissing(sourcePushSubscriptions, target.PushSubscriptions, targetPushSubscriptions.Keys.ToHashSet());
        var announcements = ImportMissing(sourceAnnouncements, target.Announcements, targetAnnouncements.Keys.ToHashSet());
        var interactions = ImportMissing(sourceInteractions, target.AnnouncementUserInteractions, targetInteractions.Keys.ToHashSet());
        var templates = ImportMissing(sourceTemplates, target.EmailTemplates, targetTemplates.Keys.ToHashSet());
        var campaigns = ImportMissing(sourceCampaigns, target.EmailCampaigns, targetCampaigns.Keys.ToHashSet());
        var campaignLogs = ImportMissing(sourceCampaignLogs, target.EmailCampaignLogs, targetCampaignLogs.Keys.ToHashSet());

        var imported = notifications + preferences + typePreferences + pushSubscriptions
            + announcements + interactions + templates + campaigns + campaignLogs;
        await owned.SaveChangesAsync(cancellationToken);

        return new OwnedNotificationBackfillResult(
            sourceNotifications.Count,
            sourcePreferences.Count,
            sourceTypePreferences.Count,
            sourcePushSubscriptions.Count,
            sourceAnnouncements.Count,
            sourceInteractions.Count,
            sourceTemplates.Count,
            sourceCampaigns.Count,
            sourceCampaignLogs.Count,
            imported);
    }

    private static void SyncAudit<TEntity>(
        IEnumerable<TEntity> source,
        IReadOnlyDictionary<Guid, TEntity> target)
        where TEntity : LegacyNotificationBase
    {
        foreach (var sourceItem in source)
        {
            if (!target.TryGetValue(sourceItem.Id, out var targetItem))
                continue;

            targetItem.CreatedBy = sourceItem.CreatedBy;
            targetItem.UpdatedBy = sourceItem.UpdatedBy;
            targetItem.DeletedAt = sourceItem.DeletedAt;
            targetItem.DeletedBy = sourceItem.DeletedBy;
        }
    }

    private static int ImportMissing<TEntity>(
        IEnumerable<TEntity> source,
        DbSet<TEntity> target,
        ISet<Guid> existingIds)
        where TEntity : class
    {
        var imported = 0;
        foreach (var item in source)
        {
            var id = item switch
            {
                LegacyUserNotification notification => notification.Id,
                LegacyNotificationPreference preference => preference.Id,
                LegacyNotificationTypePreference typePreference => typePreference.Id,
                LegacyPushSubscription pushSubscription => pushSubscription.Id,
                LegacyAnnouncement announcement => announcement.Id,
                LegacyAnnouncementUserInteraction interaction => interaction.Id,
                LegacyEmailTemplate template => template.Id,
                LegacyEmailCampaign campaign => campaign.Id,
                LegacyEmailCampaignLog campaignLog => campaignLog.Id,
                _ => throw new InvalidOperationException($"Unsupported notification entity type: {typeof(TEntity).Name}")
            };

            if (existingIds.Add(id))
            {
                target.Add(item);
                imported++;
            }
        }

        return imported;
    }
}

public sealed record OwnedNotificationBackfillResult(
    int SourceNotificationCount,
    int SourcePreferenceCount,
    int SourceTypePreferenceCount,
    int SourcePushSubscriptionCount,
    int SourceAnnouncementCount,
    int SourceInteractionCount,
    int SourceTemplateCount,
    int SourceCampaignCount,
    int SourceCampaignLogCount,
    int ImportedCount);
