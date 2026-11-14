using EduPlatform.Shared.Contracts.Events.SpeedReading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpeedReading.Application.Subscription;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Infrastructure.Legacy;

/// <summary>
/// Billing parity (14.11.2026): an individual subscriber is reminded seven days and one day before the subscription
/// ends and told once when it has ended, as in Koçluk. Nothing is sent while a renewal cannot be bought (bank transfer
/// closed or the seller details missing), for subscriptions an institution licence gives, or when another active
/// subscription already continues past the end date. Subscriptions themselves are not changed.
/// </summary>
internal sealed class SpeedReadingSubscriptionLifecycle(
    OwnedSpeedReadingDbContext db,
    ISpeedReadingSubscriptionNotices? notices,
    TimeProvider clock) : ISpeedReadingSubscriptionLifecycle
{
    private const int BatchSize = 500;
    private static readonly TimeSpan Window = TimeSpan.FromDays(7);

    public async Task<int> ProcessAsync(CancellationToken cancellationToken = default)
    {
        // Without a message bus there is nobody to tell.
        if (notices is null) return 0;
        var now = clock.GetUtcNow().UtcDateTime;
        var settings = await db.Set<LegacyBankTransferPaymentSettings>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == LegacySpeedReadingSubscription.BankTransferSettingsId, cancellationToken);
        if (settings is null
            || !BankTransferPaymentRules.HasCompletePublicSettings(settings.IsEnabled, settings.AccountHolder, settings.BankName, settings.Iban)
            || !SpeedReadingSalesService.Seller(settings).IsComplete)
            return 0;

        var attempt = 0;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // One instance at a time: two workers must not send the same reminder.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtextextended('speed-reading-subscription-lifecycle', 0))", cancellationToken);
            var since = now - Window;
            var until = now + Window;
            var rows = await (from subscription in db.Set<LegacyUserSubscription>().AsNoTracking()
                              join plan in db.Set<LegacySubscriptionPlan>().AsNoTracking() on subscription.PlanId equals plan.Id
                              where subscription.Status == "Active" && !subscription.IsDeleted && subscription.InstitutionAccessLicenseId == null
                                 && subscription.EndDate != null && subscription.EndDate >= since && subscription.EndDate <= until
                              orderby subscription.EndDate, subscription.Id
                              select new { Subscription = subscription, plan.Name }).Take(BatchSize).ToListAsync(cancellationToken);
            var sent = 0;
            foreach (var row in rows)
            {
                var subscription = row.Subscription;
                var end = subscription.EndDate!.Value;
                var stage = end <= now ? "expired" : end - now <= TimeSpan.FromDays(1) ? "1d" : "7d";
                var key = $"{stage}:{end:yyyyMMddHHmm}";
                if (await db.Set<LegacySubscriptionNoticeReceipt>().AnyAsync(item => item.SubscriptionId == subscription.Id && item.Stage == key, cancellationToken))
                    continue;
                // Another active subscription of the same product already runs past this end: nothing lapses.
                if (await db.Set<LegacyUserSubscription>().AsNoTracking().AnyAsync(other => other.Id != subscription.Id
                        && other.UserId == subscription.UserId && other.ProductId == subscription.ProductId && other.Status == "Active"
                        && !other.IsDeleted && (other.EndDate == null || other.EndDate > end), cancellationToken))
                    continue;
                var receipt = new LegacySubscriptionNoticeReceipt { Id = Guid.NewGuid(), SubscriptionId = subscription.Id, Stage = key, CreatedAt = now };
                db.Add(receipt);
                await notices.StageAsync(new(subscription.UserId, subscription.UserEmail,
                    stage == "expired" ? SpeedReadingSubscriptionNoticeKinds.Expired : SpeedReadingSubscriptionNoticeKinds.Expiring,
                    row.Name, end, null, now, receipt.Id), cancellationToken);
                sent++;
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return sent;
        });
    }
}

/// <summary>Runs the subscription reminders every hour.</summary>
public sealed class SpeedReadingSubscriptionLifecycleWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SpeedReadingSubscriptionLifecycleWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sent = await scope.ServiceProvider.GetRequiredService<ISpeedReadingSubscriptionLifecycle>().ProcessAsync(stoppingToken);
                if (sent > 0) logger.LogInformation("Staged {Count} speed-reading subscription reminders.", sent);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
            catch (Exception exception)
            {
                // Reminders must never take down the API; the next interval retries.
                logger.LogWarning(exception, "Speed-reading subscription reminders failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
