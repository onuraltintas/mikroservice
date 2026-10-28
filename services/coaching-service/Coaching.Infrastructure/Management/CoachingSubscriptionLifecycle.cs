using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Contracts.Events.Coaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coaching.Infrastructure.Management;

/// <summary>
/// Marks subscriptions whose end date has passed as Expired and tells their owners: a reminder seven days and one day
/// before the end, and a notice when it has ended. A renewal that already covers the end date keeps the owner quiet.
/// Reminder receipts make every notice happen once; subscriptions that lapsed long before this job ran are only marked.
/// </summary>
public sealed class CoachingSubscriptionLifecycleProcessor(CoachingDbContext db, ICoachingEventPublisher events, TimeProvider clock)
{
    public const string ExpiringKind = "SubscriptionExpiring";
    public const string ExpiredKind = "SubscriptionExpired";
    private const int BatchSize = 500;
    private static readonly TimeSpan ExpiredNoticeWindow = TimeSpan.FromDays(7);

    public async Task<int> ProcessAsync(CancellationToken ct = default)
    {
        if (!db.Database.IsRelational()) return 0;
        var now = clock.GetUtcNow().UtcDateTime;
        var attempt = 0;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtextextended('coaching-subscription-lifecycle', 0))", ct);
            var notices = await ExpireAsync(now, ct) + await RemindAsync(now, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return notices;
        });
    }

    private async Task<int> ExpireAsync(DateTime now, CancellationToken ct)
    {
        var lapsed = await (from subscription in db.CoachingSubscriptions
                            join plan in db.CoachingSubscriptionPlans on subscription.PlanId equals plan.Id
                            where subscription.Status == "Active" && subscription.EndDate <= now
                            orderby subscription.EndDate, subscription.Id
                            select new { Subscription = subscription, plan.Name, plan.Audience })
            .Take(BatchSize).ToListAsync(ct);
        var sent = 0;
        foreach (var row in lapsed)
        {
            var subscription = row.Subscription;
            subscription.Status = "Expired";
            subscription.UpdatedAt = now;
            if (subscription.EndDate < now - ExpiredNoticeWindow || await IsCoveredAsync(subscription, row.Audience, ct)) continue;
            var kind = subscription.InstitutionId.HasValue
                ? CoachingSubscriptionNoticeKinds.InstitutionExpired
                : CoachingSubscriptionNoticeKinds.Expired;
            if (await NoticeOnceAsync(subscription, row.Name, row.Audience, ExpiredKind, "expired", kind, now, ct)) sent++;
        }
        return sent;
    }

    private async Task<int> RemindAsync(DateTime now, CancellationToken ct)
    {
        var horizon = now.AddDays(7);
        var ending = await (from subscription in db.CoachingSubscriptions.AsNoTracking()
                            join plan in db.CoachingSubscriptionPlans.AsNoTracking() on subscription.PlanId equals plan.Id
                            where subscription.Status == "Active" && subscription.EndDate > now && subscription.EndDate <= horizon
                            orderby subscription.EndDate, subscription.Id
                            select new { Subscription = subscription, plan.Name, plan.Audience })
            .Take(BatchSize).ToListAsync(ct);
        var sent = 0;
        foreach (var row in ending)
        {
            var stage = row.Subscription.EndDate - now <= TimeSpan.FromDays(1) ? "1d" : "7d";
            if (await IsCoveredAsync(row.Subscription, row.Audience, ct)) continue;
            var kind = row.Subscription.InstitutionId.HasValue
                ? CoachingSubscriptionNoticeKinds.InstitutionExpiring
                : CoachingSubscriptionNoticeKinds.Expiring;
            if (await NoticeOnceAsync(row.Subscription, row.Name, row.Audience, ExpiringKind, stage, kind, now, ct)) sent++;
        }
        return sent;
    }

    private async Task<bool> NoticeOnceAsync(CoachingSubscription subscription, string planName, string audience, string receiptKind,
        string stage, string noticeKind, DateTime now, CancellationToken ct)
    {
        var recipient = subscription.UserId ?? subscription.InstitutionId!.Value;
        var receipt = CoachingReminderReceipt.Create(recipient, subscription.Id, receiptKind,
            $"{stage}:{subscription.EndDate:yyyyMMddHHmm}", subscription.EndDate, now);
        if (db.CoachingReminderReceipts.Local.Any(x => x.Id == receipt.Id)
            || await db.CoachingReminderReceipts.AnyAsync(x => x.Id == receipt.Id, ct)) return false;
        db.CoachingReminderReceipts.Add(receipt);
        await events.PublishAsync(new CoachingSubscriptionNoticeV1(receipt.Id, subscription.UserId,
            string.IsNullOrWhiteSpace(subscription.UserEmail) ? null : subscription.UserEmail,
            noticeKind, audience, planName, subscription.EndDate, null, now), ct);
        return true;
    }

    /// <summary>Another active subscription of the same kind continues from this one's end date.</summary>
    private Task<bool> IsCoveredAsync(CoachingSubscription subscription, string audience, CancellationToken ct)
    {
        var others = from other in db.CoachingSubscriptions.AsNoTracking()
                     join plan in db.CoachingSubscriptionPlans.AsNoTracking() on other.PlanId equals plan.Id
                     where other.Id != subscription.Id && other.Status == "Active" && plan.Audience == audience
                         && other.StartDate <= subscription.EndDate && other.EndDate > subscription.EndDate
                     select other;
        others = subscription.UserId is { } userId
            ? others.Where(other => other.UserId == userId)
            : others.Where(other => other.InstitutionId == subscription.InstitutionId);
        return others.AnyAsync(ct);
    }
}

public sealed class CoachingSubscriptionLifecycleWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<CoachingSubscriptionLifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<CoachingSubscriptionLifecycleProcessor>().ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Coaching subscription expiry could not be processed; the next cycle will retry."); }
            await Task.Delay(TimeSpan.FromHours(1), clock, stoppingToken);
        }
    }
}
