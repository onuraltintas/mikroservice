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
                UpdatedAt = preference.UpdatedAt,
                IsDeleted = preference.IsDeleted
            })
            .ToList();
        var sourcePushSubscriptions = await legacy.PushSubscriptions.AsNoTracking().ToListAsync(cancellationToken);
        var sourceAnnouncements = await legacy.Announcements.AsNoTracking().ToListAsync(cancellationToken);
        var sourceInteractions = await legacy.AnnouncementUserInteractions.AsNoTracking().ToListAsync(cancellationToken);
        var sourceTemplates = await legacy.EmailTemplates.AsNoTracking().ToListAsync(cancellationToken);
        var sourceCampaigns = await legacy.EmailCampaigns.AsNoTracking().ToListAsync(cancellationToken);
        var sourceCampaignLogs = await legacy.EmailCampaignLogs.AsNoTracking().ToListAsync(cancellationToken);

        var announcementIds = sourceAnnouncements.Select(item => item.Id).ToHashSet();
        var campaignIds = sourceCampaigns.Select(item => item.Id).ToHashSet();
        if (sourceInteractions.Any(item => !announcementIds.Contains(item.AnnouncementId)))
            throw new InvalidOperationException("Announcement interaction references a missing announcement.");
        if (sourceCampaignLogs.Any(item => !campaignIds.Contains(item.CampaignId)))
            throw new InvalidOperationException("Email campaign log references a missing campaign.");

        var target = (ISpeedReadingDataContext)owned;
        var targetNotificationIds = await target.Notifications.Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var targetPreferenceIds = await target.NotificationPreferences.Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var targetTypePreferenceIds = await target.NotificationTypePreferences.Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var targetPushSubscriptionIds = await target.PushSubscriptions.Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var targetAnnouncementIds = await target.Announcements.Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var targetInteractionIds = await target.AnnouncementUserInteractions.Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var targetTemplateIds = await target.EmailTemplates.Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var targetCampaignIds = await target.EmailCampaigns.Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var targetCampaignLogIds = await target.EmailCampaignLogs.Select(item => item.Id).ToHashSetAsync(cancellationToken);

        var notifications = ImportMissing(sourceNotifications, target.Notifications, targetNotificationIds);
        var preferences = ImportMissing(sourcePreferences, target.NotificationPreferences, targetPreferenceIds);
        var typePreferences = ImportMissing(sourceTypePreferences, target.NotificationTypePreferences, targetTypePreferenceIds);
        var pushSubscriptions = ImportMissing(sourcePushSubscriptions, target.PushSubscriptions, targetPushSubscriptionIds);
        var announcements = ImportMissing(sourceAnnouncements, target.Announcements, targetAnnouncementIds);
        var interactions = ImportMissing(sourceInteractions, target.AnnouncementUserInteractions, targetInteractionIds);
        var templates = ImportMissing(sourceTemplates, target.EmailTemplates, targetTemplateIds);
        var campaigns = ImportMissing(sourceCampaigns, target.EmailCampaigns, targetCampaignIds);
        var campaignLogs = ImportMissing(sourceCampaignLogs, target.EmailCampaignLogs, targetCampaignLogIds);

        var imported = notifications + preferences + typePreferences + pushSubscriptions
            + announcements + interactions + templates + campaigns + campaignLogs;
        if (imported > 0)
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
