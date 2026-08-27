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
    DbSet<LegacyProduct> Products { get; }
    DbSet<LegacySubscriptionPlan> SubscriptionPlans { get; }
    DbSet<LegacyUserSubscription> UserSubscriptions { get; }
    DbSet<LegacyPayment> Payments { get; }
    EntityEntry Entry(object entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
