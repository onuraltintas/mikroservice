using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure.Persistence;

internal interface ISpeedReadingDataContext
{
    DatabaseFacade Database { get; }
    DbSet<LegacyContentBlock> ContentBlocks { get; }
    DbSet<LegacyPage> Pages { get; }
    DbSet<LegacyBlogPost> BlogPosts { get; }
    DbSet<LegacyContactMessage> ContactMessages { get; }
    DbSet<LegacyNewsletterSubscriber> NewsletterSubscribers { get; }
    DbSet<LegacyCmsMediaAsset> CmsMediaAssets { get; }
    DbSet<LegacyCmsNavigationItem> CmsNavigationItems { get; }
    DbSet<LegacyProduct> Products { get; }
    DbSet<LegacySubscriptionPlan> SubscriptionPlans { get; }
    DbSet<LegacyUserSubscription> UserSubscriptions { get; }
    DbSet<LegacyPayment> Payments { get; }
    DbSet<LegacyUserNotification> Notifications { get; }
    DbSet<LegacyNotificationPreference> NotificationPreferences { get; }
    DbSet<LegacyNotificationTypePreference> NotificationTypePreferences { get; }
    DbSet<LegacyPushSubscription> PushSubscriptions { get; }
    DbSet<LegacyAnnouncement> Announcements { get; }
    DbSet<LegacyAnnouncementUserInteraction> AnnouncementUserInteractions { get; }
    DbSet<LegacyEmailTemplate> EmailTemplates { get; }
    DbSet<LegacyEmailCampaign> EmailCampaigns { get; }
    DbSet<LegacyEmailCampaignLog> EmailCampaignLogs { get; }
    DbSet<LegacyRsvpSession> RsvpSessions { get; }
    EntityEntry Entry(object entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
