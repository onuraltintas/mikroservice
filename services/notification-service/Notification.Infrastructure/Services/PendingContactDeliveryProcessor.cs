using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Services;

/// <summary>
/// Coaching audit K3: sends the e-mail and text copies whose recipient address could not be looked up while Identity was
/// unavailable. Rows are claimed with <c>FOR UPDATE SKIP LOCKED</c>, so several instances never send the same copy; the
/// copy keeps the id of the normal path. A copy still undeliverable after two days is dropped (the in-app notice stays).
/// </summary>
public sealed class PendingContactDeliveryProcessor(NotificationDbContext db, IRecipientDirectory directory,
    NotificationChannelPolicy policy, TimeProvider clock, ILogger<PendingContactDeliveryProcessor>? logger = null)
{
    public const int BatchSize = 100;

    public async Task<int> ProcessAsync(CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return await ProcessBatchAsync(ct);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var sent = await ProcessBatchAsync(ct);
                await transaction.CommitAsync(ct);
                return sent;
            }
            finally { db.ChangeTracker.Clear(); }
        });
    }

    private async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var due = db.Database.IsRelational()
            ? await db.PendingContactDeliveries.FromSqlInterpolated($"""
                SELECT * FROM "PendingContactDeliveries"
                WHERE "NextAttemptAt" <= {now}
                ORDER BY "NextAttemptAt"
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """).AsTracking().ToListAsync(ct)
            : await db.PendingContactDeliveries.Where(x => x.NextAttemptAt <= now).OrderBy(x => x.NextAttemptAt).Take(BatchSize).ToListAsync(ct);
        if (due.Count == 0) return 0;

        var expired = due.Where(x => x.Expired(now)).ToArray();
        if (expired.Length > 0)
        {
            db.PendingContactDeliveries.RemoveRange(expired);
            logger?.LogWarning("{Count} notification copies were dropped: the recipient address stayed unavailable for two days.", expired.Length);
        }
        var open = due.Except(expired).ToArray();
        var sent = 0;
        if (open.Length > 0)
        {
            IReadOnlyDictionary<Guid, RecipientContact>? contacts = null;
            try { contacts = await directory.GetAsync(open.Select(x => x.UserId).Distinct().ToArray(), ct); }
            catch (RecipientDirectoryUnavailableException error)
            {
                foreach (var row in open) row.Retry(now, error.Message);
            }
            if (contacts is not null)
            {
                foreach (var row in open)
                {
                    await policy.DeliverPendingAsync(row, contacts, ct);
                    db.PendingContactDeliveries.Remove(row);
                    sent++;
                }
            }
        }
        await db.SaveChangesAsync(ct);
        return sent;
    }
}

public sealed class PendingContactDeliveryWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<PendingContactDeliveryWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<PendingContactDeliveryProcessor>().ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Deferred notification copies could not be processed; the next cycle will retry."); }
            await Task.Delay(Interval, clock, stoppingToken);
        }
    }
}
