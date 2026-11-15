using EduPlatform.Shared.Contracts.Billing;
using EduPlatform.Shared.Contracts.Events.Billing;
using MassTransit;

namespace Coaching.Application.Subscriptions;

/// <summary>
/// Whether a student may use Koçluk, as the billing service ("Abonelik ve Ödeme") sees it.
/// <paramref name="Known"/> false: billing could not be asked and nothing was remembered, so the student is let in.
/// </summary>
public sealed record CoachingBillingDecision(bool Allowed, bool RequireSubscription, DateTime? EndsAt, bool Known);

/// <summary>Asks the billing service about a student's access to Koçluk and remembers the answer for a short time.</summary>
public interface ICoachingBillingAccess
{
    /// <summary>False: this deployment runs without the billing service and Koçluk's own records decide.</summary>
    bool Enabled { get; }
    Task<CoachingBillingDecision> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    /// <summary>The user's entitlements changed: the next question goes to billing again.</summary>
    void Forget(Guid userId);
    /// <summary>The product's "subscription required" switch changed: everything remembered is dropped.</summary>
    void ForgetAll();
}

public static class CoachingBillingRules
{
    /// <summary>The entitlement of a student: bought with an individual plan, or given with a seat by a teacher or an institution.</summary>
    public const string StudentEntitlement = "coaching.student";

    /// <summary>A missing answer (billing does not sell the product) and a product that asks for no subscription are both open.</summary>
    public static CoachingBillingDecision Decide(BillingProductAccessResponse? access, DateTime now)
    {
        if (access is null || !access.RequireSubscription) return new(true, false, null, true);
        var ends = (access.Entitlements ?? []).Where(item => item.Key == StudentEntitlement && item.EndsAt > now)
            .Select(item => (DateTime?)item.EndsAt).DefaultIfEmpty(null).Max();
        return new(ends is not null, true, ends, true);
    }

    public static CoachingSubscriptionAccessSummary Summary(CoachingBillingDecision decision) =>
        new(decision.Allowed, decision.RequireSubscription, null, decision is { Allowed: true, RequireSubscription: true } ? "Active" : null, decision.EndsAt, []);
}

/// <summary>A payment was approved or reversed, a seat given or taken: what was remembered about the user is dropped.</summary>
public sealed class CoachingBillingEntitlementsChangedConsumer(ICoachingBillingAccess access) : IConsumer<BillingEntitlementsChangedV1>
{
    public Task Consume(ConsumeContext<BillingEntitlementsChangedV1> context)
    {
        if (context.Message.ProductCodes.Contains(BillingProductCodes.Coaching)) access.Forget(context.Message.UserId);
        return Task.CompletedTask;
    }
}

/// <summary>"Subscription required" was switched for the product: every remembered answer is dropped.</summary>
public sealed class CoachingBillingSettingsChangedConsumer(ICoachingBillingAccess access) : IConsumer<BillingProductSettingsChangedV1>
{
    public Task Consume(ConsumeContext<BillingProductSettingsChangedV1> context)
    {
        if (context.Message.ProductCode == BillingProductCodes.Coaching) access.ForgetAll();
        return Task.CompletedTask;
    }
}
