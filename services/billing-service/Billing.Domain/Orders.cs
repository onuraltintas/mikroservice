namespace Billing.Domain;

public enum AccountType
{
    User = 0,
    /// <summary>An Identity institution; opened with seats and institution plans (stage 4).</summary>
    Organization = 1
}

public enum OrderStatus
{
    /// <summary>Waiting for the money to arrive and be approved.</summary>
    Pending = 0,
    Paid = 1,
    Rejected = 2,
    /// <summary>No approved payment within the waiting time.</summary>
    Expired = 3,
    /// <summary>Taken back by the buyer before it was reviewed.</summary>
    Withdrawn = 4,
    /// <summary>The approval was reversed by an admin.</summary>
    Cancelled = 5
}

public enum OrderKind
{
    /// <summary>A plan for a period.</summary>
    Subscription = 0,
    /// <summary>Extra seats for the rest of a running institution subscription.</summary>
    ExtraSeats = 1
}

public enum PaymentMethod
{
    BankTransfer = 0
}

public enum PaymentStatus
{
    Approved = 0,
    Reversed = 1
}

public enum SubscriptionStatus
{
    Active = 0,
    Cancelled = 1
}

public enum PeriodSource
{
    Payment = 0,
    Manual = 1,
    Trial = 2
}

public enum PeriodStatus
{
    Active = 0,
    Cancelled = 1
}

/// <summary>Who buys: a user, or (from stage 4) an organization. One row per buyer, shared by every product.</summary>
public sealed class Account
{
    public Guid Id { get; set; }
    public AccountType Type { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// One attempt to buy a plan. What was bought is copied in when the order is opened, so a later change of the plan
/// changes neither the price nor the time nor the access of an order already given.
/// </summary>
public sealed class Order
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid PlanId { get; set; }
    public OrderKind Kind { get; set; }
    /// <summary>For extra seats: the subscription they are added to and how many.</summary>
    public Guid? SubscriptionId { get; set; }
    public int? ExtraSeats { get; set; }
    public Guid OrderedByUserId { get; set; }
    /// <summary>
    /// Whose access the order pays for: the buyer, the child a parent pays for, or, for an organization account, the
    /// organization itself (its Identity institution id).
    /// </summary>
    public Guid BeneficiaryUserId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public BuyerType BuyerType { get; set; }
    public decimal ListPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = BillingRules.DefaultCurrency;
    public int DurationDays { get; set; }
    public int? SeatCount { get; set; }
    /// <summary>JSON array of the plan's entitlements when the order was opened (see <see cref="OrderEntitlement"/>).</summary>
    public string EntitlementsJson { get; set; } = "[]";
    /// <summary>What the buyer writes in the transfer description, e.g. EDU-7K4Q2M. The same for every bank account.</summary>
    public string PaymentCode { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public string DocumentsVersion { get; set; } = string.Empty;
    /// <summary>JSON of the pre-information form and the distance-sales contract exactly as accepted.</summary>
    public string DocumentsJson { get; set; } = "{}";
    public DateTime AcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    /// <summary>When the buyer said "I have paid"; null until then.</summary>
    public DateTime? PaidDeclaredAt { get; set; }
    public string? PayerNote { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>One line of <see cref="Order.EntitlementsJson"/>.</summary>
public sealed record OrderEntitlement(Guid EntitlementId, string Key, string ProductCode, EntitlementHolder Holder, int? LimitValue);

/// <summary>The receipt (dekont) a buyer may attach; kept in the database, like invoices.</summary>
public sealed class OrderReceipt
{
    public Guid OrderId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
    public int Size { get; set; }
    public DateTime UploadedAt { get; set; }
}

/// <summary>Money received for an order. A financial record: never deleted, only reversed.</summary>
public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = BillingRules.DefaultCurrency;
    /// <summary>The admin who approved; null when a bank transaction matched by itself (stage 3).</summary>
    public Guid? ApprovedBy { get; set; }
    public DateTime ApprovedAt { get; set; }
    public Guid? BankTransactionId { get; set; }
    public Guid? ReversedBy { get; set; }
    public DateTime? ReversedAt { get; set; }
    public string? ReversalReason { get; set; }
}

/// <summary>An account's access through a plan, for one beneficiary. Its time is the sum of its periods.</summary>
public sealed class Subscription
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid PlanId { get; set; }
    public Guid BeneficiaryUserId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public SubscriptionStatus Status { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int? SeatCount { get; set; }
    /// <summary>
    /// Seats bought on top of the plan's. They were priced for the days left when they were bought, so they last until
    /// <see cref="ExtraSeatsUntil"/> (the end of the subscription at that time) and are not carried into a renewal.
    /// </summary>
    public int ExtraSeats { get; set; }
    public DateTime? ExtraSeatsUntil { get; set; }
    /// <summary>The entitlements of the latest approved order; what the grants are built from.</summary>
    public string EntitlementsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class SubscriptionPeriod
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }
    public PeriodSource Source { get; set; }
    public Guid? PaymentId { get; set; }
    public PeriodStatus Status { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}

/// <summary>Who holds which entitlement until when. The only table the entitlement query of the products reads.</summary>
public sealed class EntitlementGrant
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid EntitlementId { get; set; }
    public Guid ProductId { get; set; }
    public string Key { get; set; } = string.Empty;
    public int? LimitValue { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public Guid SubscriptionId { get; set; }
}

/// <summary>
/// A seat of a subscription given to a user. A seat taken back is suspended, not removed: who had it and who took it
/// back stays on record, and giving it again reuses the row.
/// </summary>
public sealed class Seat
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }
    public Guid UserId { get; set; }
    public bool IsSuspended { get; set; }
    public string? SuspensionReason { get; set; }
    public Guid AssignedBy { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
