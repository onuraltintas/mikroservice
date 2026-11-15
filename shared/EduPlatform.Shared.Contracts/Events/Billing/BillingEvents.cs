namespace EduPlatform.Shared.Contracts.Events.Billing;

/// <summary>
/// A user's entitlements in these products changed (a payment was approved or reversed, a subscription ended by an
/// admin). Product services drop what they cached for the user and ask the billing service again.
/// </summary>
public sealed record BillingEntitlementsChangedV1(Guid UserId, string[] ProductCodes, DateTime ChangedAt);

/// <summary>The "subscription required" switch of a product was turned on or off.</summary>
public sealed record BillingProductSettingsChangedV1(string ProductCode, bool RequireSubscription, DateTime ChangedAt);
