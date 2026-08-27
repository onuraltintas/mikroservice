using Microsoft.EntityFrameworkCore;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingSubscriptionBackfill(SpeedReadingDbContext legacy, OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedSubscriptionBackfillResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var sourceProducts = await legacy.Products.AsNoTracking().ToListAsync(cancellationToken);
        var sourcePlans = await legacy.SubscriptionPlans.AsNoTracking().ToListAsync(cancellationToken);
        var sourceSubscriptions = await legacy.UserSubscriptions.AsNoTracking().ToListAsync(cancellationToken);
        var sourcePayments = await legacy.Payments.AsNoTracking().ToListAsync(cancellationToken);
        var productIds = sourceProducts.Select(item => item.Id).ToHashSet();
        var planIds = sourcePlans.Select(item => item.Id).ToHashSet();
        if (sourcePlans.Any(item => !productIds.Contains(item.ProductId))) throw new InvalidOperationException("Subscription plan references a missing product.");
        if (sourceSubscriptions.Any(item => !planIds.Contains(item.PlanId) || !productIds.Contains(item.ProductId))) throw new InvalidOperationException("Subscription references a missing plan or product.");
        if (sourcePayments.Any(item => !planIds.Contains(item.PlanId))) throw new InvalidOperationException("Payment references a missing plan.");

        var target = (ISpeedReadingDataContext)owned;
        var targetProductIds = await target.Products.Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var targetPlanIds = await target.SubscriptionPlans.Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var targetSubscriptionIds = await target.UserSubscriptions.Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var targetPaymentIds = await target.Payments.Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var products = 0; var plans = 0; var subscriptions = 0; var payments = 0;
        foreach (var item in sourceProducts.Where(item => !targetProductIds.Contains(item.Id))) { target.Products.Add(item); products++; }
        foreach (var item in sourcePlans.Where(item => !targetPlanIds.Contains(item.Id))) { target.SubscriptionPlans.Add(item); plans++; }
        foreach (var item in sourceSubscriptions.Where(item => !targetSubscriptionIds.Contains(item.Id))) { target.UserSubscriptions.Add(item); subscriptions++; }
        foreach (var item in sourcePayments.Where(item => !targetPaymentIds.Contains(item.Id))) { target.Payments.Add(item); payments++; }
        if (products + plans + subscriptions + payments > 0) await owned.SaveChangesAsync(cancellationToken);
        return new OwnedSubscriptionBackfillResult(sourceProducts.Count, sourcePlans.Count, sourceSubscriptions.Count, sourcePayments.Count, products, plans, subscriptions, payments);
    }
}

public sealed record OwnedSubscriptionBackfillResult(
    int SourceProductCount,
    int SourcePlanCount,
    int SourceSubscriptionCount,
    int SourcePaymentCount,
    int ImportedProductCount,
    int ImportedPlanCount,
    int ImportedSubscriptionCount,
    int ImportedPaymentCount);
