namespace EduPlatform.Shared.Contracts.Events.Billing;

/// <summary>
/// A user's entitlements in these products changed (a payment was approved or reversed, a subscription ended by an
/// admin). Product services drop what they cached for the user and ask the billing service again.
/// </summary>
public sealed record BillingEntitlementsChangedV1(Guid UserId, string[] ProductCodes, DateTime ChangedAt);

/// <summary>The "subscription required" switch of a product was turned on or off.</summary>
/// <summary><paramref name="StaffSeatsRequired"/>: students free, a new student of a teacher or an institution takes a seat (23.11.2026).</summary>
public sealed record BillingProductSettingsChangedV1(string ProductCode, bool RequireSubscription, DateTime ChangedAt, bool StaffSeatsRequired = false);

/// <summary>
/// Something a buyer (or the seller) is told about by e-mail: a decision on an order, an answer to a withdrawal
/// request, an invoice, a subscription about to end or ended. <see cref="RecipientEmail"/> is the buyer's address, or
/// the seller's alert address for the kinds in <see cref="BillingNoticeKinds.IsForSeller"/>. <see cref="RecipientUserId"/>
/// is null for the seller and for an organization account.
/// </summary>
public sealed record BillingNoticeV1(
    Guid NoticeId,
    Guid? RecipientUserId,
    string RecipientEmail,
    string Kind,
    string[] ProductCodes,
    string PlanName,
    DateTime? EndsAt,
    string? Detail,
    string? PaymentCode,
    decimal? Amount,
    string? Currency,
    DateTime OccurredAt);

public static class BillingNoticeKinds
{
    /// <summary>To the seller: a buyer says an order is paid.</summary>
    public const string PaymentDeclared = "PaymentDeclared";
    /// <summary>To the seller: a buyer asks to withdraw.</summary>
    public const string WithdrawalRequested = "WithdrawalRequested";
    public const string OrderApproved = "OrderApproved";
    public const string OrderRejected = "OrderRejected";
    public const string ApprovalReversed = "ApprovalReversed";
    /// <summary>A waiting order dropped after its waiting time.</summary>
    public const string OrderExpired = "OrderExpired";
    public const string WithdrawalApproved = "WithdrawalApproved";
    public const string WithdrawalRejected = "WithdrawalRejected";
    public const string InvoiceReady = "InvoiceReady";
    /// <summary>Seven days and one day before a subscription ends, and when it has ended.</summary>
    public const string Expiring = "Expiring";
    public const string ExpiringTomorrow = "ExpiringTomorrow";
    public const string Expired = "Expired";

    public static bool IsForSeller(string kind) => kind is PaymentDeclared or WithdrawalRequested;
}
