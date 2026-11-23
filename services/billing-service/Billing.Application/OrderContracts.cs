using Billing.Domain;

namespace Billing.Application;

/// <summary>Who is asking, from the access token. The token carries no name, so the buyer's name comes with the order.</summary>
public sealed record BillingUser(Guid Id, string? Email, IReadOnlyCollection<string> Roles);

/// <summary><paramref name="OrganizationId"/>: the institution an institution plan is bought for; the buyer must administer it.</summary>
public sealed record CreateOrderRequest(Guid PlanId, string? DocumentsVersion, string? BuyerName, Guid? OrganizationId = null, string? CouponCode = null);

public sealed record CreateSeatOrderRequest(int Count, string? DocumentsVersion, string? BuyerName);

/// <summary>An institution as Identity knows it.</summary>
public sealed record Institution(Guid Id, string Name);

/// <summary>Asks Identity which institutions a user administers; billing keeps no membership of its own.</summary>
public interface IBillingInstitutions
{
    Task<IReadOnlyList<Institution>> GetAdministeredAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record SeatView(Guid UserId, bool IsSuspended, DateTime AssignedAt,
    /// <summary>The seat is within the capacity and gives access now.</summary>
    bool IsCovered);

public sealed record SeatPoolView(
    Guid SubscriptionId,
    string PlanName,
    DateTime EndsAt,
    int Capacity,
    int Used,
    int ExtraSeats,
    DateTime? ExtraSeatsUntil,
    /// <summary>The plan sells extra seats at this price for a whole period; null when it sells none.</summary>
    decimal? ExtraSeatPrice,
    IReadOnlyList<SeatView> Seats);

public sealed record SeatQuote(int Count, decimal Amount, string Currency, int DaysLeft, SalesDocuments Documents);

/// <summary>Who owns a seat pool: a user (a teacher) or an organization. Exactly one is set.</summary>
public sealed record SeatOwner(Guid? UserId, Guid? OrganizationId);

public sealed record SeatChangeRequest(string Product, Guid? OwnerUserId, Guid? OrganizationId, Guid UserId, Guid ActorId, string? Reason);

/// <summary><paramref name="Code"/>: Assigned, AlreadyAssigned, Released, NoPool, PoolFull, NotAssigned.</summary>
public sealed record SeatChangeResult(bool Done, string Code, SeatPoolView? Pool);

public sealed record MyOrganization(Guid Id, string Name, IReadOnlyList<SubscriptionView> Subscriptions, IReadOnlyList<OrderView> PendingOrders);

/// <summary>Seat pools of teacher and institution subscriptions, and the extra seats an institution buys (stage 4).</summary>
public interface IBillingSeats
{
    /// <summary>The institutions the user administers, with what each has bought.</summary>
    Task<IReadOnlyList<MyOrganization>> GetMyOrganizationsAsync(BillingUser user, CancellationToken cancellationToken = default);
    /// <summary>The seats of a subscription for its owner; null when it is not the caller's.</summary>
    Task<SeatPoolView?> GetMySeatsAsync(BillingUser user, Guid subscriptionId, CancellationToken cancellationToken = default);
    Task<SeatQuote?> QuoteSeatOrderAsync(BillingUser user, Guid subscriptionId, int count, string? buyerName, CancellationToken cancellationToken = default);
    Task<OrderView?> CreateSeatOrderAsync(BillingUser user, Guid subscriptionId, CreateSeatOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>For product services: the running pool of an owner in a product; null when there is none.</summary>
    Task<SeatPoolView?> GetPoolAsync(SeatOwner owner, string productCode, CancellationToken cancellationToken = default);
    /// <summary>For product services, which have already checked that the user may sit there (membership, assignment).</summary>
    Task<SeatChangeResult> AssignAsync(SeatChangeRequest request, CancellationToken cancellationToken = default);
    Task<SeatChangeResult> ReleaseAsync(SeatChangeRequest request, CancellationToken cancellationToken = default);

    Task<SeatPoolView?> GetSeatsAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
    Task<SeatPoolView?> AdminReleaseAsync(Guid subscriptionId, Guid userId, Guid actorId, CancellationToken cancellationToken = default);
}

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
    string DocumentsVersion,
    OrderKind Kind = OrderKind.Subscription,
    int? ExtraSeats = null,
    decimal ListPrice = 0,
    decimal Discount = 0,
    string? CouponCode = null,
    /// <summary>The state of the buyer's withdrawal (cayma) request, if there is one.</summary>
    WithdrawalStatus? Withdrawal = null,
    /// <summary>A paid order can be withdrawn from until this moment; null when it cannot.</summary>
    DateTime? WithdrawUntil = null,
    bool HasInvoice = false,
    /// <summary>Null when the price included taxes; otherwise the VAT rate of the order.</summary>
    decimal? VatRate = null,
    decimal VatAmount = 0);

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
    IReadOnlyList<GrantedEntitlement> Entitlements,
    /// <summary>
    /// The latest end of any subscription that gave the user this product, past or running; null when there was none.
    /// Course Management deletes a workspace's data a year after it (user decision 20.11.2026).
    /// </summary>
    DateTime? LastEndsAt = null,
    /// <summary>Students free; a new student of an independent teacher or an institution takes a seat of their plan.</summary>
    bool StaffSeatsRequired = false);

/// <summary>"Aboneliğim" of one product for the signed-in user.</summary>
public sealed record MyBilling(
    string ProductCode,
    string ProductName,
    bool RequireSubscription,
    bool SalesOpen,
    IReadOnlyList<GrantedEntitlement> Entitlements,
    IReadOnlyList<SubscriptionView> Subscriptions,
    IReadOnlyList<OrderView> PendingOrders,
    bool BusinessSales = false,
    /// <summary>Students free, teachers and institutions pay: their plans are on sale even though no subscription is required.</summary>
    bool StaffSeatsRequired = false);

/// <summary>Publishes billing messages with the transaction that causes them (outbox).</summary>
public interface IBillingEvents
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;
}

/// <summary>Orders by bank transfer, their approval, the subscriptions they open and the entitlements those give (stage 2).</summary>
public interface IBillingOrders
{
    /// <summary>The documents for a plan the buyer is about to order; null when the plan is unknown.</summary>
    Task<SalesDocuments?> PreviewDocumentsAsync(BillingUser user, Guid planId, string? buyerName, string? couponCode = null, CancellationToken cancellationToken = default);
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

public sealed record StatementPreview(IReadOnlyList<IReadOnlyList<string>> Rows, int TotalRows, StatementMapping? Suggested);

public sealed record BankTransactionView(
    Guid Id,
    Guid BankAccountId,
    string BankName,
    DateTime BookedAt,
    decimal Amount,
    string Description,
    string? Sender,
    string? PaymentCode,
    BankTransactionStatus Status,
    Guid? OrderId,
    /// <summary>The buyer and plan of the tied order, for the admin's eye.</summary>
    string? OrderSummary,
    string? Note);

public sealed record StatementImportView(
    Guid Id,
    Guid BankAccountId,
    string BankName,
    string FileName,
    DateTime UploadedAt,
    int RowCount,
    int AddedCount,
    int DuplicateCount,
    int MatchedCount,
    int AutoApprovedCount);

public sealed record StatementImportResult(StatementImportView Import, IReadOnlyList<BankTransactionView> Transactions);

public sealed record TieTransactionRequest(Guid OrderId);

/// <summary>Bank statements: upload, matching by payment code and amount, automatic approval per product (stage 3).</summary>
public interface IBillingBankStatements
{
    /// <summary>The first rows of an uploaded file and the columns guessed from its header, before anything is stored.</summary>
    StatementPreview Preview(string fileName, byte[] content);
    Task<StatementImportResult> ImportAsync(Guid bankAccountId, string fileName, byte[] content, StatementMapping mapping, Guid actorId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StatementImportView>> GetImportsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BankTransactionView>> GetTransactionsAsync(BankTransactionStatus? status, CancellationToken cancellationToken = default);
    /// <summary>An admin ties a line to a waiting order of the same amount and approves it. Null when either is unknown.</summary>
    Task<BankTransactionView?> ApproveWithOrderAsync(Guid transactionId, Guid orderId, Guid actorId, CancellationToken cancellationToken = default);
    Task<BankTransactionView?> SetIgnoredAsync(Guid transactionId, bool ignored, string? note, Guid actorId, CancellationToken cancellationToken = default);
}

public sealed record CouponView(
    Guid Id,
    string Code,
    CouponKind Kind,
    decimal Value,
    DateTime? StartsAt,
    DateTime? EndsAt,
    int? MaxUses,
    /// <summary>Waiting and paid orders that carry the code.</summary>
    int UsedCount,
    IReadOnlyList<Guid> PlanIds,
    bool IsActive);

public sealed record CouponRequest(string Code, CouponKind Kind, decimal Value, DateTime? StartsAt, DateTime? EndsAt, int? MaxUses, IReadOnlyList<Guid>? PlanIds, bool IsActive);

public sealed record CouponQuoteRequest(Guid PlanId, string? Code);
public sealed record CouponQuote(string Code, decimal ListPrice, decimal Discount, decimal Amount, string Currency,
    decimal? VatRate = null, decimal VatAmount = 0);

public sealed record StartTrialRequest(Guid PlanId, string? BuyerName);

public sealed record WithdrawalRequest(string? Reason);
public sealed record ReviewWithdrawalRequest(bool Approve, string? Note);

public sealed record WithdrawalView(
    Guid Id,
    Guid OrderId,
    WithdrawalStatus Status,
    string? Reason,
    DateTime RequestedAt,
    string? ReviewNote,
    DateTime? ReviewedAt,
    string AccountName,
    string? AccountEmail,
    string PlanName,
    string PaymentCode,
    decimal Amount,
    string Currency);

public sealed record InvoiceUpload(string Number, DateOnly IssuedOn, string FileName, byte[] Content);
public sealed record InvoiceFile(string Number, string FileName, byte[] Content);

/// <summary>Coupons, free trials, withdrawal (cayma) and invoices (stage 5).</summary>
public interface IBillingExtras
{
    Task<IReadOnlyList<CouponView>> GetCouponsAsync(CancellationToken cancellationToken = default);
    Task<CouponView> CreateCouponAsync(CouponRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<CouponView?> UpdateCouponAsync(Guid id, CouponRequest request, Guid actorId, CancellationToken cancellationToken = default);
    /// <summary>What a plan costs the caller with a code; throws why when the code cannot be used.</summary>
    Task<CouponQuote> QuoteCouponAsync(BillingUser user, CouponQuoteRequest request, CancellationToken cancellationToken = default);

    /// <summary>Starts the plan's free trial for the caller: once, and only for somebody who never had the product.</summary>
    Task<SubscriptionView> StartTrialAsync(BillingUser user, StartTrialRequest request, CancellationToken cancellationToken = default);

    Task<OrderView?> RequestWithdrawalAsync(Guid userId, Guid orderId, string? reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WithdrawalView>> GetWithdrawalsAsync(WithdrawalStatus? status, CancellationToken cancellationToken = default);
    Task<WithdrawalView?> ReviewWithdrawalAsync(Guid id, ReviewWithdrawalRequest request, Guid actorId, CancellationToken cancellationToken = default);

    Task<AdminOrderView?> AttachInvoiceAsync(Guid orderId, InvoiceUpload upload, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> RemoveInvoiceAsync(Guid orderId, CancellationToken cancellationToken = default);
    /// <summary>The invoice for the buyer, or for an admin (<paramref name="userId"/> null).</summary>
    Task<InvoiceFile?> GetInvoiceAsync(Guid? userId, Guid orderId, CancellationToken cancellationToken = default);
}

public sealed record ErasureAssessment(int Orders, int Payments, int Subscriptions, int Seats, int Invoices);

/// <summary>
/// Reminders about subscriptions that end, and what happens to the billing records of an erased account: access ends
/// at once, the financial records stay ten years, then what names the person is removed (stage 5).
/// </summary>
public interface IBillingLifecycle
{
    /// <summary>Sends the reminders that are due (7 days and 1 day before the end, and at the end); returns how many.</summary>
    Task<int> SendRemindersAsync(CancellationToken cancellationToken = default);
    Task<ErasureAssessment> AssessErasureAsync(Guid userId, CancellationToken cancellationToken = default);
    /// <summary>Ends the access of an erased account and starts the retention time; returns how many records changed. Safe to repeat.</summary>
    Task<int> EraseUserAsync(Guid userId, CancellationToken cancellationToken = default);
    /// <summary>Removes what names the person from records whose retention time is over; returns how many accounts.</summary>
    Task<int> RedactExpiredAsync(CancellationToken cancellationToken = default);
    /// <summary>Clears the sender's name, description and note of bank lines tied to no order, a year after the movement.</summary>
    Task<int> ClearUnmatchedBankLinesAsync(CancellationToken cancellationToken = default);
}

/// <summary>What the admin sees first: what waits for a decision, what ends soon, what was approved this month.</summary>
public sealed record BillingOverview(
    int WaitingOrders,
    /// <summary>Of the waiting orders, those whose buyer said the money was sent.</summary>
    int DeclaredOrders,
    int WithdrawalRequests,
    int UnmatchedTransactions,
    int CurrentSubscriptions,
    decimal ApprovedThisMonth,
    decimal ApprovedLastMonth,
    string Currency,
    /// <summary>Current subscriptions that end within 14 days, soonest first (at most 50).</summary>
    IReadOnlyList<AdminSubscriptionView> EndingSoon);

/// <summary>A subscription the admin gives by hand. Exactly one of <paramref name="UserId"/> and <paramref name="OrganizationId"/> is set.</summary>
public sealed record GrantSubscriptionRequest(Guid? UserId, Guid? OrganizationId, string? DisplayName, string? Email, Guid PlanId, int? Days, string? Note);
public sealed record ExtendSubscriptionRequest(int Days, string? Note);
public sealed record CancelSubscriptionRequest(string? Note);

/// <summary>The admin's overview and the subscriptions given, extended and ended by hand (stage 6).</summary>
public interface IBillingAdministration
{
    Task<BillingOverview> GetOverviewAsync(string? productCode, CancellationToken cancellationToken = default);
    /// <summary>Gives a plan without a payment; on a running subscription of the same plan the time is added to its end.</summary>
    Task<AdminSubscriptionView> GrantAsync(GrantSubscriptionRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<AdminSubscriptionView?> ExtendAsync(Guid subscriptionId, ExtendSubscriptionRequest request, Guid actorId, CancellationToken cancellationToken = default);
    /// <summary>Ends a subscription now. Payments stay as they are: money is handled by reversing an approval or by a withdrawal.</summary>
    Task<AdminSubscriptionView?> CancelAsync(Guid subscriptionId, CancelSubscriptionRequest request, Guid actorId, CancellationToken cancellationToken = default);
}
