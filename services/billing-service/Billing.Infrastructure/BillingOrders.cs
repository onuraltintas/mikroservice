using System.Security.Cryptography;
using System.Text.Json;
using Billing.Application;
using Billing.Domain;
using EduPlatform.Shared.Contracts.Events.Billing;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure;

/// <summary>
/// Buying by bank transfer: an order with its payment code and accepted documents, "I have paid", the admin's approval
/// or refusal, the subscription and periods an approval opens, and the entitlement grants the products ask for.
/// </summary>
public sealed class BillingOrders(BillingDbContext db, IBillingEvents events, TimeProvider clock) : IBillingOrders
{
    public const int MaxListedOrders = 500;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---- Buyer --------------------------------------------------------------------------------------------------------

    public async Task<SalesDocuments?> PreviewDocumentsAsync(BillingUser user, Guid planId, string? buyerName, CancellationToken cancellationToken = default)
    {
        var plan = await db.Plans.AsNoTracking().SingleOrDefaultAsync(item => item.Id == planId, cancellationToken);
        if (plan is null) return null;
        var offer = await OfferAsync(plan, user, cancellationToken);
        return SalesDocumentTexts.Render(Terms(offer, plan, BuyerName(buyerName), user.Email, plan.Price, Now));
    }

    public async Task<OrderView> CreateOrderAsync(BillingUser user, CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        var plan = await db.Plans.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.PlanId, cancellationToken)
            ?? throw new BusinessRuleException("Billing.PlanNotFound", "Plan bulunamadı.");
        var offer = await OfferAsync(plan, user, cancellationToken);
        var buyerName = BuyerName(request.BuyerName);
        if (!string.Equals(request.DocumentsVersion?.Trim(), SalesDocumentTexts.Version, StringComparison.Ordinal))
            throw new BusinessRuleException("Billing.DocumentsNotAccepted",
                "Devam etmek için ön bilgilendirme formunu ve mesafeli satış sözleşmesini onaylayın.");

        var now = Now;
        var account = await db.Accounts.SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (account is null)
        {
            account = new Account { Id = Guid.NewGuid(), Type = AccountType.User, UserId = user.Id, CreatedAt = now };
            db.Accounts.Add(account);
        }
        else if (await db.Orders.AnyAsync(item => item.AccountId == account.Id && item.PlanId == plan.Id
            && item.BeneficiaryUserId == user.Id && item.Status == OrderStatus.Pending, cancellationToken))
        {
            throw new BusinessRuleException("Billing.OrderPending", "Bu plan için ödeme bekleyen bir siparişiniz var.");
        }
        if (account.DisplayName != buyerName || account.Email != user.Email)
        {
            account.DisplayName = buyerName;
            account.Email = user.Email;
            account.UpdatedAt = now;
        }

        var documents = SalesDocumentTexts.Render(Terms(offer, plan, buyerName, user.Email, plan.Price, now));
        var order = new Order
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            PlanId = plan.Id,
            OrderedByUserId = user.Id,
            BeneficiaryUserId = user.Id,
            PlanName = plan.Name,
            BuyerType = plan.BuyerType,
            ListPrice = plan.Price,
            Discount = 0,
            Amount = plan.Price,
            Currency = plan.Currency,
            DurationDays = plan.DurationDays,
            SeatCount = plan.SeatCount,
            EntitlementsJson = JsonSerializer.Serialize(offer.Entitlements, Json),
            PaymentCode = await NewPaymentCodeAsync(cancellationToken),
            Status = OrderStatus.Pending,
            DocumentsVersion = documents.Version,
            DocumentsJson = JsonSerializer.Serialize(documents, Json),
            AcceptedAt = now,
            CreatedAt = now,
            ExpiresAt = now.AddDays(BillingRules.PendingOrderDays)
        };
        db.Orders.Add(order);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // The same buyer sent the request twice at once: one account, one waiting order per plan.
            throw new BusinessRuleException("Billing.OrderPending", "Bu plan için ödeme bekleyen bir siparişiniz var.");
        }
        return View(order, false);
    }

    public async Task<IReadOnlyList<OrderView>> GetMyOrdersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var orders = await db.Orders.AsNoTracking().Where(item => item.OrderedByUserId == userId || item.BeneficiaryUserId == userId)
            .OrderByDescending(item => item.CreatedAt).Take(MaxListedOrders).ToListAsync(cancellationToken);
        return await ViewsAsync(orders, cancellationToken);
    }

    public async Task<OrderView?> DeclarePaidAsync(Guid userId, Guid orderId, string? note, ReceiptUpload? receipt, CancellationToken cancellationToken = default)
    {
        var payerNote = note?.Trim();
        if (payerNote is { Length: > 500 }) throw new BusinessRuleException("Billing.OrderInvalid", "Not en fazla 500 karakter olabilir.");
        string? contentType = null;
        if (receipt is not null)
        {
            if (receipt.Content.Length is 0 or > BillingRules.MaxReceiptBytes)
                throw new BusinessRuleException("Billing.ReceiptInvalid", "Dekont en fazla 5 MB olabilir.");
            contentType = BillingRules.ReceiptContentType(receipt.Content)
                ?? throw new BusinessRuleException("Billing.ReceiptInvalid", "Dekont PDF, JPG ya da PNG olmalıdır.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await LockOrderAsync(orderId, cancellationToken);
        if (order is null || order.OrderedByUserId != userId) return null;
        var now = Now;
        EnsurePending(order, now);
        order.PaidDeclaredAt ??= now;
        order.PayerNote = string.IsNullOrEmpty(payerNote) ? order.PayerNote : payerNote;
        order.UpdatedAt = now;
        var existing = await db.OrderReceipts.SingleOrDefaultAsync(item => item.OrderId == order.Id, cancellationToken);
        if (receipt is not null)
        {
            if (existing is null) db.OrderReceipts.Add(existing = new OrderReceipt { OrderId = order.Id });
            existing.FileName = SafeFileName(receipt.FileName, contentType!);
            existing.ContentType = contentType!;
            existing.Content = receipt.Content;
            existing.Size = receipt.Content.Length;
            existing.UploadedAt = now;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return View(order, existing is not null);
    }

    public async Task<OrderView?> WithdrawOrderAsync(Guid userId, Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await LockOrderAsync(orderId, cancellationToken);
        if (order is null || order.OrderedByUserId != userId) return null;
        if (order.Status != OrderStatus.Pending)
            throw new BusinessRuleException("Billing.OrderNotPending", "Yalnız ödeme bekleyen sipariş geri çekilebilir.");
        var now = Now;
        order.Status = OrderStatus.Withdrawn;
        order.UpdatedAt = now;
        // A receipt is personal data kept for the review only; an order taken back needs none.
        await db.OrderReceipts.Where(item => item.OrderId == order.Id).ExecuteDeleteAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return View(order, false);
    }

    public async Task<SalesDocuments?> GetAcceptedDocumentsAsync(Guid userId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var json = await db.Orders.AsNoTracking()
            .Where(item => item.Id == orderId && (item.OrderedByUserId == userId || item.BeneficiaryUserId == userId))
            .Select(item => item.DocumentsJson).SingleOrDefaultAsync(cancellationToken);
        return json is null ? null : JsonSerializer.Deserialize<SalesDocuments>(json, Json);
    }

    public async Task<ReceiptFile?> GetReceiptAsync(Guid? userId, Guid orderId, CancellationToken cancellationToken = default)
    {
        if (userId is { } owner && !await db.Orders.AnyAsync(item => item.Id == orderId && item.OrderedByUserId == owner, cancellationToken)) return null;
        var receipt = await db.OrderReceipts.AsNoTracking().SingleOrDefaultAsync(item => item.OrderId == orderId, cancellationToken);
        return receipt is null ? null : new ReceiptFile(receipt.FileName, receipt.ContentType, receipt.Content);
    }

    public async Task<MyBilling?> GetMyBillingAsync(Guid userId, string productCode, CancellationToken cancellationToken = default)
    {
        var product = await ActiveProductAsync(productCode, cancellationToken);
        if (product is null) return null;
        var now = Now;
        var subscriptions = (await db.Subscriptions.AsNoTracking().Where(item => item.BeneficiaryUserId == userId)
                .OrderByDescending(item => item.EndsAt).ToListAsync(cancellationToken))
            .Select(item => View(item, now)).Where(item => item.ProductCodes.Contains(product.Code)).ToArray();
        var pending = (await ViewsAsync(await db.Orders.AsNoTracking()
                .Where(item => (item.OrderedByUserId == userId || item.BeneficiaryUserId == userId) && item.Status == OrderStatus.Pending)
                .OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken), cancellationToken))
            .Where(item => item.ProductCodes.Contains(product.Code)).ToArray();
        return new MyBilling(product.Code, product.Name, product.RequireSubscription, await SalesOpenAsync(cancellationToken),
            await GrantsAsync(userId, product.Id, now, cancellationToken), subscriptions, pending);
    }

    // ---- Admin --------------------------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<AdminOrderView>> GetOrdersAsync(OrderStatus? status, string? productCode, CancellationToken cancellationToken = default)
    {
        var query = db.Orders.AsNoTracking();
        if (status is { } wanted) query = query.Where(item => item.Status == wanted);
        // Every waiting order is listed; of the finished ones, the newest.
        var waiting = await query.Where(item => item.Status == OrderStatus.Pending).OrderBy(item => item.CreatedAt)
            .Take(MaxListedOrders).ToListAsync(cancellationToken);
        var finished = await query.Where(item => item.Status != OrderStatus.Pending).OrderByDescending(item => item.CreatedAt)
            .Take(MaxListedOrders).ToListAsync(cancellationToken);
        // Orders the buyer says are paid come first, oldest first: that is the queue.
        var orders = waiting.Where(item => item.PaidDeclaredAt is not null).OrderBy(item => item.PaidDeclaredAt)
            .Concat(waiting.Where(item => item.PaidDeclaredAt is null))
            .Concat(finished).ToList();
        var views = await AdminViewsAsync(orders, cancellationToken);
        var code = BillingRules.NormalizeCode(productCode);
        return code is null ? views : views.Where(item => item.Order.ProductCodes.Contains(code)).ToArray();
    }

    public async Task<AdminOrderView?> ApproveOrderAsync(Guid orderId, Guid actorId, string? note, CancellationToken cancellationToken = default)
    {
        var reviewNote = Note(note, required: false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await LockOrderAsync(orderId, cancellationToken);
        if (order is null) return null;
        var now = Now;
        EnsurePending(order, now);

        var payment = new Payment
        {
            Id = Guid.NewGuid(), OrderId = order.Id, Method = PaymentMethod.BankTransfer, Status = PaymentStatus.Approved,
            Amount = order.Amount, Currency = order.Currency, ApprovedBy = actorId, ApprovedAt = now
        };
        db.Payments.Add(payment);

        var subscription = (await db.Subscriptions.FromSqlInterpolated(
                $"SELECT * FROM billing.subscriptions WHERE \"AccountId\" = {order.AccountId} AND \"PlanId\" = {order.PlanId} AND \"BeneficiaryUserId\" = {order.BeneficiaryUserId} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault();
        var running = subscription is { Status: SubscriptionStatus.Active } && subscription.EndsAt > now;
        var (startsAt, endsAt) = BillingRules.NextPeriod(now, running ? subscription!.EndsAt : null, order.DurationDays);
        if (subscription is null)
        {
            subscription = new Subscription { Id = Guid.NewGuid(), AccountId = order.AccountId, PlanId = order.PlanId,
                BeneficiaryUserId = order.BeneficiaryUserId, CreatedAt = now };
            db.Subscriptions.Add(subscription);
        }
        if (!running) subscription.StartsAt = startsAt;
        subscription.Status = SubscriptionStatus.Active;
        subscription.EndsAt = endsAt;
        subscription.PlanName = order.PlanName;
        subscription.SeatCount = order.SeatCount;
        subscription.EntitlementsJson = order.EntitlementsJson;
        subscription.UpdatedAt = now;
        db.SubscriptionPeriods.Add(new SubscriptionPeriod { Id = Guid.NewGuid(), SubscriptionId = subscription.Id, Source = PeriodSource.Payment,
            PaymentId = payment.Id, Status = PeriodStatus.Active, StartsAt = startsAt, EndsAt = endsAt, CreatedAt = now });

        order.Status = OrderStatus.Paid;
        order.ReviewedBy = actorId;
        order.ReviewedAt = now;
        order.ReviewNote = reviewNote;
        order.UpdatedAt = now;

        await RebuildGrantsAsync(subscription, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await AdminViewsAsync([order], cancellationToken))[0];
    }

    public async Task<AdminOrderView?> RejectOrderAsync(Guid orderId, Guid actorId, string? note, CancellationToken cancellationToken = default)
    {
        var reviewNote = Note(note, required: true);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await LockOrderAsync(orderId, cancellationToken);
        if (order is null) return null;
        var now = Now;
        if (order.Status != OrderStatus.Pending)
            throw new BusinessRuleException("Billing.OrderNotPending", "Bu sipariş daha önce sonuçlandı.");
        order.Status = OrderStatus.Rejected;
        order.ReviewedBy = actorId;
        order.ReviewedAt = now;
        order.ReviewNote = reviewNote;
        order.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await AdminViewsAsync([order], cancellationToken))[0];
    }

    public async Task<AdminOrderView?> ReverseApprovalAsync(Guid orderId, Guid actorId, string? reason, CancellationToken cancellationToken = default)
    {
        var note = Note(reason, required: true)!;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await LockOrderAsync(orderId, cancellationToken);
        if (order is null) return null;
        if (order.Status != OrderStatus.Paid)
            throw new BusinessRuleException("Billing.OrderNotPaid", "Yalnız onaylanmış ödemenin onayı geri alınabilir.");
        var now = Now;
        var payment = await db.Payments.SingleAsync(item => item.OrderId == order.Id, cancellationToken);
        payment.Status = PaymentStatus.Reversed;
        payment.ReversedBy = actorId;
        payment.ReversedAt = now;
        payment.ReversalReason = note.Length > 1_000 ? note[..1_000] : note;

        var period = await db.SubscriptionPeriods.SingleOrDefaultAsync(item => item.PaymentId == payment.Id && item.Status == PeriodStatus.Active, cancellationToken);
        if (period is not null)
        {
            var subscription = (await db.Subscriptions.FromSqlInterpolated($"SELECT * FROM billing.subscriptions WHERE \"Id\" = {period.SubscriptionId} FOR UPDATE")
                .ToListAsync(cancellationToken)).Single();
            await CancelPeriodAsync(subscription, period, now, cancellationToken);
            await RebuildGrantsAsync(subscription, now, cancellationToken);
        }

        order.Status = OrderStatus.Cancelled;
        var line = $"Onay geri alındı ({now:yyyy-MM-dd HH:mm} UTC): {note}";
        order.ReviewNote = string.IsNullOrWhiteSpace(order.ReviewNote) ? line : $"{order.ReviewNote}\n{line}";
        if (order.ReviewNote.Length > 2_000) order.ReviewNote = order.ReviewNote[..2_000];
        order.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await AdminViewsAsync([order], cancellationToken))[0];
    }

    public async Task<IReadOnlyList<AdminSubscriptionView>> GetSubscriptionsAsync(string? productCode, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var code = BillingRules.NormalizeCode(productCode);
        var rows = await (from subscription in db.Subscriptions.AsNoTracking()
                          join account in db.Accounts.AsNoTracking() on subscription.AccountId equals account.Id
                          orderby subscription.EndsAt descending
                          select new { subscription, account.DisplayName, account.Email }).Take(MaxListedOrders).ToListAsync(cancellationToken);
        return rows.Select(row => new AdminSubscriptionView(View(row.subscription, now), row.DisplayName, row.Email, row.subscription.BeneficiaryUserId))
            .Where(item => code is null || item.Subscription.ProductCodes.Contains(code)).ToArray();
    }

    // ---- Products and background --------------------------------------------------------------------------------------

    public async Task<ProductAccess?> GetAccessAsync(Guid userId, string productCode, CancellationToken cancellationToken = default)
    {
        var product = await ActiveProductAsync(productCode, cancellationToken);
        if (product is null) return null;
        return new ProductAccess(product.Code, product.RequireSubscription, await GrantsAsync(userId, product.Id, Now, cancellationToken));
    }

    public async Task<int> ExpirePendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        var now = Now;
        var expired = await db.Orders.Where(item => item.Status == OrderStatus.Pending && item.ExpiresAt <= now)
            .OrderBy(item => item.ExpiresAt).Take(200).ToListAsync(cancellationToken);
        foreach (var order in expired)
        {
            order.Status = OrderStatus.Expired;
            order.UpdatedAt = now;
        }
        if (expired.Count == 0) return 0;
        var ids = expired.Select(item => item.Id).ToArray();
        await db.SaveChangesAsync(cancellationToken);
        await db.OrderReceipts.Where(item => ids.Contains(item.OrderId)).ExecuteDeleteAsync(cancellationToken);
        return expired.Count;
    }

    // ---- Rules --------------------------------------------------------------------------------------------------------

    private sealed record Offer(Seller Seller, IReadOnlyList<OrderEntitlement> Entitlements, IReadOnlyList<string> ProductNames);

    /// <summary>Everything that must hold before a plan can be ordered; throws what is missing in the buyer's language.</summary>
    private async Task<Offer> OfferAsync(Plan plan, BillingUser user, CancellationToken cancellationToken)
    {
        if (!plan.IsActive || !BillingRules.CanBeOrdered(plan.Price, plan.IsContactOnly))
            throw new BusinessRuleException("Billing.PlanNotOnSale", "Bu plan satın alınamıyor.");
        if (plan.BuyerType == BuyerType.Institution)
            throw new BusinessRuleException("Billing.PlanNotOnSale", "Kurum planları için bizimle iletişime geçin.");
        if (plan.BuyerType == BuyerType.Teacher && !user.Roles.Contains("Teacher"))
            throw new BusinessRuleException("Billing.PlanNotForBuyer", "Bu planı yalnız öğretmenler satın alabilir.");

        var rows = await (from link in db.PlanEntitlements.AsNoTracking()
                          join entitlement in db.Entitlements.AsNoTracking() on link.EntitlementId equals entitlement.Id
                          join product in db.Products.AsNoTracking() on entitlement.ProductId equals product.Id
                          where link.PlanId == plan.Id
                          orderby product.SortOrder, entitlement.Key, link.Holder
                          select new { link, entitlement, product }).ToListAsync(cancellationToken);
        if (rows.Count == 0 || rows.Any(row => !row.product.IsActive))
            throw new BusinessRuleException("Billing.PlanNotOnSale", "Bu plan satın alınamıyor.");
        // A product that asks for no subscription sells nothing; a bundle is on sale when one of its products does.
        if (rows.All(row => !row.product.RequireSubscription))
            throw new BusinessRuleException("Billing.SubscriptionNotRequired", "Bu üründe abonelik gerekmiyor; satın alma kapalı.");
        var seller = await db.Sellers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == Seller.SingleId, cancellationToken);
        if (seller is not { IsComplete: true } || !await db.BankAccounts.AnyAsync(item => item.IsActive, cancellationToken))
            throw new BusinessRuleException("Billing.SalesClosed", "Satın alma şu anda kapalı. Lütfen daha sonra tekrar deneyin.");
        return new Offer(seller,
            rows.Select(row => new OrderEntitlement(row.entitlement.Id, row.entitlement.Key, row.product.Code, row.link.Holder, row.link.LimitValue)).ToArray(),
            rows.Select(row => row.product.Name).Distinct().ToArray());
    }

    private async Task<bool> SalesOpenAsync(CancellationToken cancellationToken) =>
        await db.Sellers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == Seller.SingleId, cancellationToken) is { IsComplete: true }
        && await db.BankAccounts.AnyAsync(item => item.IsActive, cancellationToken);

    private static SalesTerms Terms(Offer offer, Plan plan, string buyerName, string? email, decimal amount, DateTime date) =>
        new(offer.Seller, buyerName, email, plan.Name, offer.ProductNames, plan.DurationDays, amount, plan.Currency, date);

    private static string BuyerName(string? value)
    {
        var name = value?.Trim();
        return name is { Length: >= 3 and <= 200 }
            ? name
            : throw new BusinessRuleException("Billing.BuyerNameInvalid", "Sözleşme için adınızı ve soyadınızı yazın.");
    }

    private static string? Note(string? value, bool required)
    {
        var note = value?.Trim();
        if (string.IsNullOrEmpty(note))
            return required ? throw new BusinessRuleException("Billing.NoteRequired", "Gerekçe yazın (5–1000 karakter).") : null;
        return note.Length is < 5 or > 1_000
            ? throw new BusinessRuleException("Billing.NoteRequired", "Not 5–1000 karakter olmalıdır.")
            : note;
    }

    private static void EnsurePending(Order order, DateTime now)
    {
        if (order.Status != OrderStatus.Pending)
            throw new BusinessRuleException("Billing.OrderNotPending", "Bu sipariş daha önce sonuçlandı.");
        if (order.ExpiresAt <= now)
            throw new BusinessRuleException("Billing.OrderExpired", "Bu siparişin bekleme süresi doldu; yeni sipariş açılmalıdır.");
    }

    /// <summary>
    /// Removes the unused part of a reversed payment's period. Periods paid after it move forward by that much, so a
    /// renewal keeps its full length; the subscription ends when nothing paid is left (carried over from Koçluk).
    /// </summary>
    private async Task CancelPeriodAsync(Subscription subscription, SubscriptionPeriod period, DateTime now, CancellationToken cancellationToken)
    {
        var originalEnd = period.EndsAt;
        var usedUntil = now < period.StartsAt ? period.StartsAt : now > originalEnd ? originalEnd : now;
        var removed = originalEnd - usedUntil;
        period.EndsAt = usedUntil;
        period.Status = PeriodStatus.Cancelled;
        period.CancelledAt = now;
        if (removed > TimeSpan.Zero)
        {
            foreach (var later in await db.SubscriptionPeriods.Where(item => item.SubscriptionId == subscription.Id
                && item.Status == PeriodStatus.Active && item.Id != period.Id && item.StartsAt >= originalEnd).ToListAsync(cancellationToken))
            {
                later.StartsAt -= removed;
                later.EndsAt -= removed;
            }
            if (subscription.EndsAt >= originalEnd) subscription.EndsAt -= removed;
        }
        if (subscription.EndsAt < subscription.StartsAt) subscription.EndsAt = subscription.StartsAt;
        if (subscription.EndsAt <= now) subscription.Status = SubscriptionStatus.Cancelled;
        subscription.UpdatedAt = now;
    }

    /// <summary>
    /// The grants of a subscription are rebuilt whenever it changes: the buyer-side entitlements of its latest order, for
    /// its beneficiary, until it ends. Seat holders get theirs when seats are given out (stage 4).
    /// </summary>
    private async Task RebuildGrantsAsync(Subscription subscription, DateTime now, CancellationToken cancellationToken)
    {
        var entitlements = JsonSerializer.Deserialize<OrderEntitlement[]>(subscription.EntitlementsJson, Json) ?? [];
        db.EntitlementGrants.RemoveRange(await db.EntitlementGrants.Where(item => item.SubscriptionId == subscription.Id).ToListAsync(cancellationToken));
        if (subscription.Status == SubscriptionStatus.Active && subscription.EndsAt > now)
        {
            var ids = entitlements.Select(item => item.EntitlementId).Distinct().ToArray();
            var productIds = await db.Entitlements.AsNoTracking().Where(item => ids.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.ProductId, cancellationToken);
            foreach (var entitlement in entitlements.Where(item => item.Holder == EntitlementHolder.Buyer && productIds.ContainsKey(item.EntitlementId)))
            {
                db.EntitlementGrants.Add(new EntitlementGrant { Id = Guid.NewGuid(), UserId = subscription.BeneficiaryUserId,
                    EntitlementId = entitlement.EntitlementId, ProductId = productIds[entitlement.EntitlementId], Key = entitlement.Key,
                    LimitValue = entitlement.LimitValue, StartsAt = subscription.StartsAt, EndsAt = subscription.EndsAt, SubscriptionId = subscription.Id });
            }
        }
        var productCodes = entitlements.Select(item => item.ProductCode).Distinct().ToArray();
        if (productCodes.Length > 0)
            await events.PublishAsync(new BillingEntitlementsChangedV1(subscription.BeneficiaryUserId, productCodes, now), cancellationToken);
    }

    private async Task<IReadOnlyList<GrantedEntitlement>> GrantsAsync(Guid userId, Guid productId, DateTime now, CancellationToken cancellationToken)
    {
        var grants = await db.EntitlementGrants.AsNoTracking()
            .Where(item => item.UserId == userId && item.ProductId == productId && item.StartsAt <= now && item.EndsAt > now)
            .ToListAsync(cancellationToken);
        // The same entitlement through two subscriptions: the longer time and the larger limit count.
        return grants.GroupBy(item => item.Key).OrderBy(group => group.Key)
            .Select(group => new GrantedEntitlement(group.Key, group.Max(item => item.LimitValue), group.Max(item => item.EndsAt))).ToArray();
    }

    // ---- Helpers ------------------------------------------------------------------------------------------------------

    /// <summary>The row lock makes a review single-shot: a second approval waits, then finds the order no longer pending.</summary>
    private async Task<Order?> LockOrderAsync(Guid id, CancellationToken cancellationToken) =>
        (await db.Orders.FromSqlInterpolated($"SELECT * FROM billing.orders WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(cancellationToken)).SingleOrDefault();

    private async Task<Product?> ActiveProductAsync(string productCode, CancellationToken cancellationToken)
    {
        var code = BillingRules.NormalizeCode(productCode);
        return code is null ? null : await db.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Code == code && item.IsActive, cancellationToken);
    }

    private async Task<string> NewPaymentCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = BillingRules.NewPaymentCode(RandomNumberGenerator.GetInt32);
            if (!await db.Orders.AnyAsync(item => item.PaymentCode == code, cancellationToken)) return code;
        }
        throw new InvalidOperationException("No free payment code was found.");
    }

    private static string SafeFileName(string? name, string contentType)
    {
        var raw = name ?? string.Empty;
        var cut = raw.LastIndexOfAny(['/', '\\']);
        var file = new string((cut >= 0 ? raw[(cut + 1)..] : raw).Where(character => !char.IsControl(character) && character != '"').ToArray()).Trim();
        if (file.Length > 150) file = file[^150..];
        return file.Length > 0 ? file : contentType switch { "application/pdf" => "dekont.pdf", "image/png" => "dekont.png", _ => "dekont.jpg" };
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation };

    private static string[] ProductCodes(string entitlementsJson) =>
        (JsonSerializer.Deserialize<OrderEntitlement[]>(entitlementsJson, Json) ?? []).Select(item => item.ProductCode).Distinct().ToArray();

    private static OrderView View(Order order, bool hasReceipt) =>
        new(order.Id, order.PlanId, order.PlanName, ProductCodes(order.EntitlementsJson), order.BuyerType, order.Amount, order.Currency,
            order.DurationDays, order.PaymentCode, order.Status, order.CreatedAt, order.ExpiresAt, order.PaidDeclaredAt, order.PayerNote,
            hasReceipt, order.ReviewedAt, order.ReviewNote, order.DocumentsVersion);

    private static SubscriptionView View(Subscription subscription, DateTime now) =>
        new(subscription.Id, subscription.PlanId, subscription.PlanName, ProductCodes(subscription.EntitlementsJson), subscription.Status,
            subscription.StartsAt, subscription.EndsAt,
            subscription.Status == SubscriptionStatus.Active && subscription.StartsAt <= now && subscription.EndsAt > now, subscription.SeatCount);

    private async Task<IReadOnlyList<OrderView>> ViewsAsync(IReadOnlyList<Order> orders, CancellationToken cancellationToken)
    {
        var ids = orders.Select(item => item.Id).ToArray();
        var withReceipt = (await db.OrderReceipts.AsNoTracking().Where(item => ids.Contains(item.OrderId)).Select(item => item.OrderId)
            .ToListAsync(cancellationToken)).ToHashSet();
        return orders.Select(order => View(order, withReceipt.Contains(order.Id))).ToArray();
    }

    private async Task<IReadOnlyList<AdminOrderView>> AdminViewsAsync(IReadOnlyList<Order> orders, CancellationToken cancellationToken)
    {
        var views = await ViewsAsync(orders, cancellationToken);
        var accountIds = orders.Select(item => item.AccountId).Distinct().ToArray();
        var accounts = await db.Accounts.AsNoTracking().Where(item => accountIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        return orders.Select((order, index) => new AdminOrderView(views[index], accounts[order.AccountId].DisplayName, accounts[order.AccountId].Email,
            order.OrderedByUserId, order.BeneficiaryUserId, order.ReviewedBy)).ToArray();
    }
}

/// <summary>Publishes through MassTransit; with the bus outbox the message is stored by the same SaveChanges.</summary>
public sealed class MassTransitBillingEvents(MassTransit.IPublishEndpoint publisher) : IBillingEvents
{
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class =>
        publisher.Publish(message, cancellationToken);
}
