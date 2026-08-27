using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure.Persistence;

internal interface ISpeedReadingDataContext
{
    DatabaseFacade Database { get; }
    DbSet<LegacyProduct> Products { get; }
    DbSet<LegacySubscriptionPlan> SubscriptionPlans { get; }
    DbSet<LegacyUserSubscription> UserSubscriptions { get; }
    DbSet<LegacyPayment> Payments { get; }
    EntityEntry Entry(object entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
