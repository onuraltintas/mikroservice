using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Services;

public static class NotificationRecipientWrites
{
    public static Task<NotificationItem?> PersistAsync(NotificationDbContext db, NotificationItem item)
        => LockedAsync<NotificationItem?>(db, item.UserId, async () =>
        {
            if (await db.ErasedRecipients.AnyAsync(x => x.UserId == item.UserId)) return null;
            var existing = await db.Notifications.SingleOrDefaultAsync(x => x.Id == item.Id && x.UserId == item.UserId);
            if (existing is not null) return existing;
            db.Notifications.Add(item); await db.SaveChangesAsync(); return item;
        }, CancellationToken.None);

    public static async Task<T> LockedAsync<T>(NotificationDbContext db, Guid userId, Func<Task<T>> action, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Recipient is required.");
        if (!db.Database.IsRelational()) return await action();
        async Task<T> Run()
        {
            var key = $"notification-recipient:{userId:N}";
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
            return await action();
        }
        // MassTransit inbox/outbox already owns the consumer transaction.
        if (db.Database.CurrentTransaction is not null) return await Run();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try { var result = await Run(); await transaction.CommitAsync(ct); return result; }
            finally { db.ChangeTracker.Clear(); }
        });
    }
}
