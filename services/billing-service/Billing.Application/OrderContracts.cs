using Billing.Domain;

namespace Billing.Application;

/// <summary>Who is asking, from the access token. The token carries no name, so the buyer's name comes with the order.</summary>
public sealed record BillingUser(Guid Id, string? Email, IReadOnlyCollection<string> Roles);

public sealed record CreateOrderRequest(Guid PlanId, string? DocumentsVersion, string? BuyerName);

public sealed record OrderView(
    Guid Id,
    Guid PlanId,
    string PlanName,
    IReadOnlyList<string> ProductCodes,
    BuyerType BuyerType,
    decimal Amount,
    string Currency,
    int DurationDays,
    string PaymentCode,
    OrderStatus Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? PaidDeclaredAt,
    string? PayerNote,
    bool HasReceipt,
    DateTime? ReviewedAt,
    string? ReviewNote,
    string DocumentsVersion);

public sealed record AdminOrderView(
    OrderView Order,
    string AccountName,
    string? AccountEmail,
    Guid OrderedByUserId,
    Guid BeneficiaryUserId,
    /// <summary>The reviewer; null on a paid order means a bank transaction matched by itself.</summary>
    Guid? ReviewedBy);

public sealed record ReceiptUpload(string FileName, byte[] Content);
public sealed record ReceiptFile(string FileName, string ContentType, byte[] Content);

public sealed record ReviewOrderRequest(string? Note);

public sealed record SubscriptionView(
    Guid Id,
    Guid PlanId,
    string PlanName,
    IReadOnlyList<string> ProductCodes,
    SubscriptionStatus Status,
    DateTime StartsAt,
    DateTime EndsAt,
    /// <summary>Active, started and not yet over.</summary>
    bool IsCurrent,
    int? SeatCount);

public sealed record AdminSubscriptionView(SubscriptionView Subscription, string AccountName, string? AccountEmail, Guid BeneficiaryUserId);

public sealed record GrantedEntitlement(string Key, int? LimitValue, DateTime EndsAt);

/// <summary>What a product service needs to decide access for one user.</summary>
public sealed record ProductAccess(
    string ProductCode,
    /// <summary>False: everybody with an account uses the product; entitlements are not looked at.</summary>
    bool RequireSubscription,
    IReadOnlyList<GrantedEntitlement> Entitlements);

/// <summary>"Aboneliğim" of one product for the signed-in user.</summary>
public sealed record MyBilling(
    string ProductCode,
    string ProductName,
    bool RequireSubscription,
    bool SalesOpen,
    IReadOnlyList<GrantedEntitlement> Entitlements,
    IReadOnlyList<SubscriptionView> Subscriptions,
    IReadOnlyList<OrderView> PendingOrders);

/// <summary>Publishes billing messages with the transaction that causes them (outbox).</summary>
public interface IBillingEvents
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;
}

/// <summary>Orders by bank transfer, their approval, the subscriptions they open and the entitlements those give (stage 2).</summary>
public interface IBillingOrders
{
    /// <summary>The documents for a plan the buyer is about to order; null when the plan is unknown.</summary>
    Task<SalesDocuments?> PreviewDocumentsAsync(BillingUser user, Guid planId, string? buyerName, CancellationToken cancellationToken = default);
    Task<OrderView> CreateOrderAsync(BillingUser user, CreateOrderRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderView>> GetMyOrdersAsync(Guid userId, CancellationToken cancellationToken = default);
    /// <summary>"I have paid", with an optional note and receipt. Null when the order is not the caller's.</summary>
    Task<OrderView?> DeclarePaidAsync(Guid userId, Guid orderId, string? note, ReceiptUpload? receipt, CancellationToken cancellationToken = default);
    Task<OrderView?> WithdrawOrderAsync(Guid userId, Guid orderId, CancellationToken cancellationToken = default);
    Task<SalesDocuments?> GetAcceptedDocumentsAsync(Guid userId, Guid orderId, CancellationToken cancellationToken = default);
    /// <summary>The caller's own receipt, or any receipt for an admin (<paramref name="userId"/> null).</summary>
    Task<ReceiptFile?> GetReceiptAsync(Guid? userId, Guid orderId, CancellationToken cancellationToken = default);
    Task<MyBilling?> GetMyBillingAsync(Guid userId, string productCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminOrderView>> GetOrdersAsync(OrderStatus? status, string? productCode, CancellationToken cancellationToken = default);
    Task<AdminOrderView?> ApproveOrderAsync(Guid orderId, Guid actorId, string? note, CancellationToken cancellationToken = default);
    Task<AdminOrderView?> RejectOrderAsync(Guid orderId, Guid actorId, string? note, CancellationToken cancellationToken = default);
    /// <summary>Takes an approval back: the payment is reversed and its unused time leaves the subscription.</summary>
    Task<AdminOrderView?> ReverseApprovalAsync(Guid orderId, Guid actorId, string? reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminSubscriptionView>> GetSubscriptionsAsync(string? productCode, CancellationToken cancellationToken = default);

    /// <summary>Null when the product is unknown or inactive.</summary>
    Task<ProductAccess?> GetAccessAsync(Guid userId, string productCode, CancellationToken cancellationToken = default);
    /// <summary>Drops pending orders whose waiting time is over; returns how many.</summary>
    Task<int> ExpirePendingOrdersAsync(CancellationToken cancellationToken = default);
}
