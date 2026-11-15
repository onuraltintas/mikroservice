using EduPlatform.Shared.Contracts.Billing;
using EduPlatform.Shared.Contracts.Events.Billing;
using MassTransit;

namespace SpeedReading.Application.Subscription;

/// <summary>
/// Whether a student may train, as the billing service ("Abonelik ve Ödeme") sees it.
/// <paramref name="Known"/> false: billing could not be asked and nothing was remembered, so the student is let in.
/// </summary>
public sealed record BillingAccessDecision(bool Allowed, bool RequireSubscription, DateTime? EndsAt, bool Known);

/// <summary>Asks the billing service about a user's access to Hızlı Okuma and remembers the answer for a short time.</summary>
public interface ISpeedReadingBillingAccess
{
    /// <summary>False: this deployment runs without the billing service and the product's own records decide.</summary>
    bool Enabled { get; }
    Task<BillingAccessDecision> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    /// <summary>The user's entitlements changed: the next question goes to billing again.</summary>
    void Forget(Guid userId);
    /// <summary>The product's "subscription required" switch changed: everything remembered is dropped.</summary>
    void ForgetAll();
}

/// <summary>The one answer the access middleware and the client's access question use.</summary>
public interface ISpeedReadingAccess
{
    Task<UserAccessSummary> GetAsync(Guid userId, CancellationToken cancellationToken = default);
}

public static class SpeedReadingBillingRules
{
    public const string StudentEntitlement = "speed-reading.student";
    /// <summary>The name the client knows the product by.</summary>
    public const string ProductSlug = "hizliokuma";

    /// <summary>A missing answer (billing does not sell the product) and a product that asks for no subscription are both open.</summary>
    public static BillingAccessDecision Decide(BillingProductAccessResponse? access, DateTime now)
    {
        if (access is null || !access.RequireSubscription) return new(true, false, null, true);
        var ends = (access.Entitlements ?? []).Where(item => item.Key == StudentEntitlement && item.EndsAt > now)
            .Select(item => (DateTime?)item.EndsAt).DefaultIfEmpty(null).Max();
        return new(ends is not null, true, ends, true);
    }

    public static UserAccessSummary Summary(BillingAccessDecision decision) =>
        decision.Allowed ? new([ProductSlug], true, false) : new([], false, false);
}

/// <summary>A payment was approved or reversed, a seat given or taken: what was remembered about the user is dropped.</summary>
public sealed class SpeedReadingBillingEntitlementsChangedConsumer(ISpeedReadingBillingAccess access) : IConsumer<BillingEntitlementsChangedV1>
{
    public Task Consume(ConsumeContext<BillingEntitlementsChangedV1> context)
    {
        if (context.Message.ProductCodes.Contains(BillingProductCodes.SpeedReading)) access.Forget(context.Message.UserId);
        return Task.CompletedTask;
    }
}

/// <summary>"Subscription required" was switched for the product: every remembered answer is dropped.</summary>
public sealed class SpeedReadingBillingSettingsChangedConsumer(ISpeedReadingBillingAccess access) : IConsumer<BillingProductSettingsChangedV1>
{
    public Task Consume(ConsumeContext<BillingProductSettingsChangedV1> context)
    {
        if (context.Message.ProductCode == BillingProductCodes.SpeedReading) access.ForgetAll();
        return Task.CompletedTask;
    }
}
