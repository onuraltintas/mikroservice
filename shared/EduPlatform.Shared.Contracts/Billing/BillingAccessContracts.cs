namespace EduPlatform.Shared.Contracts.Billing;

/// <summary>
/// What the billing service answers a product service about one user (GET /internal/billing/v1/entitlements).
/// <paramref name="RequireSubscription"/> false: everybody with an account uses the product and the entitlements are not looked at.
/// </summary>
public sealed record BillingProductAccessResponse(string ProductCode, bool RequireSubscription, IReadOnlyList<BillingGrantedEntitlement>? Entitlements);

public sealed record BillingGrantedEntitlement(string Key, int? LimitValue, DateTime EndsAt);

public static class BillingProductCodes
{
    public const string Coaching = "coaching";
    public const string SpeedReading = "speed-reading";
    public const string CourseManagement = "course-management";
}
