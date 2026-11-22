using System.Text.Json;
using Billing.Application;
using Billing.Domain;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure;

/// <summary>
/// Seat pools: a teacher or institution subscription has a number of seats, each given to one user who then holds the
/// plan's seat-side entitlements. Billing counts seats; whether a user may sit in a pool (a teacher's own student, an
/// institution's member) is the product's knowledge, so seats are given and taken back by the product services.
/// </summary>
public sealed class BillingSeats(BillingDbContext db, BillingOrders orders, IBillingInstitutions institutions, TimeProvider clock) : IBillingSeats
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---- Owner --------------------------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<MyOrganization>> GetMyOrganizationsAsync(BillingUser user, CancellationToken cancellationToken = default)
    {
        var administered = await institutions.GetAdministeredAsync(user.Id, cancellationToken);
        if (administered.Count == 0) return [];
        var now = Now;
        var ids = administered.Select(item => item.Id).ToArray();
        var accounts = await db.Accounts.AsNoTracking().Where(item => item.OrganizationId != null && ids.Contains(item.OrganizationId.Value))
            .ToDictionaryAsync(item => item.OrganizationId!.Value, item => item.Id, cancellationToken);
        var accountIds = accounts.Values.ToArray();
        var subscriptions = await db.Subscriptions.AsNoTracking().Where(item => accountIds.Contains(item.AccountId))
            .OrderByDescending(item => item.EndsAt).ToListAsync(cancellationToken);
        var pending = await db.Orders.AsNoTracking().Where(item => accountIds.Contains(item.AccountId) && item.Status == OrderStatus.Pending)
            .OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        return administered.Select(institution =>
        {
            var accountId = accounts.GetValueOrDefault(institution.Id);
            return new MyOrganization(institution.Id, institution.Name,
                subscriptions.Where(item => item.AccountId == accountId).Select(item => SubscriptionOf(item, now)).ToArray(),
                pending.Where(item => item.AccountId == accountId).Select(BillingOrders.ViewOf).ToArray());
        }).ToArray();
    }

    public async Task<SeatPoolView?> GetMySeatsAsync(BillingUser user, Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var owned = await OwnedAsync(user, subscriptionId, cancellationToken);
        return owned is null ? null : await PoolAsync(owned.Value.Subscription, Now, cancellationToken);
    }

    public async Task<SeatQuote?> QuoteSeatOrderAsync(BillingUser user, Guid subscriptionId, int count, string? buyerName, CancellationToken cancellationToken = default)
    {
        var owned = await OwnedAsync(user, subscriptionId, cancellationToken);
        if (owned is null) return null;
        var terms = await SeatTermsAsync(user, owned.Value.Subscription, owned.Value.Account, count, buyerName, Now, cancellationToken);
        return new SeatQuote(count, terms.Amount, terms.Currency, terms.DaysLeft, terms.Documents);
    }

    public async Task<OrderView?> CreateSeatOrderAsync(BillingUser user, Guid subscriptionId, CreateSeatOrderRequest request, CancellationToken cancellationToken = default)
    {
        var owned = await OwnedAsync(user, subscriptionId, cancellationToken);
        if (owned is null) return null;
        var (subscription, account) = owned.Value;
        var now = Now;
        var terms = await SeatTermsAsync(user, subscription, account, request.Count, request.BuyerName, now, cancellationToken);
        if (!string.Equals(request.DocumentsVersion?.Trim(), terms.Documents.Version, StringComparison.Ordinal))
            throw new BusinessRuleException("Billing.DocumentsNotAccepted", terms.Documents.Business
                ? "Devam etmek için kurumsal abonelik sözleşmesini onaylayın."
                : "Devam etmek için ön bilgilendirme formunu ve mesafeli satış sözleşmesini onaylayın.");
        if (await db.Orders.AnyAsync(item => item.SubscriptionId == subscription.Id && item.Kind == OrderKind.ExtraSeats && item.Status == OrderStatus.Pending, cancellationToken))
            throw new BusinessRuleException("Billing.OrderPending", "Bu abonelik için ödeme bekleyen bir ek koltuk siparişi var.");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            PlanId = subscription.PlanId,
            Kind = OrderKind.ExtraSeats,
            SubscriptionId = subscription.Id,
            ExtraSeats = request.Count,
            OrderedByUserId = user.Id,
            BeneficiaryUserId = subscription.BeneficiaryUserId,
            PlanName = terms.Name,
            BuyerType = terms.BuyerType,
            ListPrice = terms.Amount - terms.VatAmount,
            Discount = 0,
            VatRate = terms.VatRate,
            VatAmount = terms.VatAmount,
            Amount = terms.Amount,
            Currency = terms.Currency,
            DurationDays = terms.DaysLeft,
            EntitlementsJson = subscription.EntitlementsJson,
            PaymentCode = await orders.PaymentCodeAsync(cancellationToken),
            Status = OrderStatus.Pending,
            DocumentsVersion = terms.Documents.Version,
            DocumentsJson = JsonSerializer.Serialize(terms.Documents, Json),
            AcceptedAt = now,
            CreatedAt = now,
            // Extra seats are worth nothing once the subscription is over.
            ExpiresAt = now.AddDays(BillingRules.PendingOrderDays) < subscription.EndsAt ? now.AddDays(BillingRules.PendingOrderDays) : subscription.EndsAt
        };
        db.Orders.Add(order);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            throw new BusinessRuleException("Billing.OrderPending", "Bu abonelik için ödeme bekleyen bir ek koltuk siparişi var.");
        }
        return BillingOrders.ViewOf(order);
    }

    /// <summary><see cref="Amount"/> is what the buyer pays, VAT included when the plan's price is net.</summary>
    private sealed record SeatTerms(string Name, decimal Amount, string Currency, int DaysLeft, SalesDocuments Documents, decimal? VatRate, decimal VatAmount,
        BuyerType BuyerType);

    /// <summary>What extra seats cost now and the documents that say so; throws when they cannot be bought.</summary>
    private async Task<SeatTerms> SeatTermsAsync(BillingUser user, Subscription subscription, Account account, int count, string? buyerName, DateTime now,
        CancellationToken cancellationToken)
    {
        if (count is < 1 or > BillingRules.MaxExtraSeatsPerOrder)
            throw new BusinessRuleException("Billing.SeatOrderInvalid", "Ek koltuk sayısı 1 ile 1000 arasında olmalıdır.");
        if (subscription.Status != SubscriptionStatus.Active || subscription.EndsAt <= now || subscription.StartsAt > now || subscription.SeatCount is null)
            throw new BusinessRuleException("Billing.SeatOrderInvalid", "Ek koltuk yalnız süren, koltuklu bir aboneliğe alınabilir.");
        var plan = await db.Plans.AsNoTracking().SingleAsync(item => item.Id == subscription.PlanId, cancellationToken);
        // KocApp uyumu (22.11.2026): a teacher's own subscription takes extra seats as well as an institution's.
        if ((account.Type != AccountType.Organization && plan.BuyerType != BuyerType.Teacher) || plan.ExtraSeatPrice is not { } price)
            throw new BusinessRuleException("Billing.SeatOrderInvalid", "Bu planda ek koltuk satılmıyor.");
        await orders.EnsureSalesOpenAsync(cancellationToken);
        // Extra seats already running end with the period they were bought in; new ones join them.
        var until = subscription.ExtraSeats > 0 && subscription.ExtraSeatsUntil is { } lasting && lasting > now ? lasting : subscription.EndsAt;
        var amount = BillingRules.ExtraSeatsPrice(price, count, plan.DurationDays, now, until);
        if (amount <= 0) throw new BusinessRuleException("Billing.SeatOrderInvalid", "Aboneliğin bitmesine çok az kaldı; ek koltuğu yenilemeyle birlikte alın.");
        var daysLeft = Math.Clamp((int)Math.Ceiling((until - now).TotalDays), 1, plan.DurationDays);
        var name = $"{subscription.PlanName} — {count} ek koltuk";
        var entitlements = JsonSerializer.Deserialize<OrderEntitlement[]>(subscription.EntitlementsJson, Json) ?? [];
        var codes = entitlements.Select(item => item.ProductCode).Distinct().ToArray();
        var products = await db.Products.AsNoTracking().Where(item => codes.Contains(item.Code)).OrderBy(item => item.SortOrder)
            .Select(item => new { item.Name, item.BusinessSales }).ToListAsync(cancellationToken);
        var vat = BillingRules.Vat(amount, plan.VatRate);
        var documents = SalesDocumentTexts.Render(new SalesTerms(await orders.SellerAsync(cancellationToken),
            account.Type == AccountType.Organization ? $"{account.DisplayName} (yetkili: {BillingOrders.RequireBuyerName(buyerName)})"
                : BillingOrders.RequireBuyerName(buyerName), user.Email, name, products.Select(item => item.Name).ToArray(),
            daysLeft, amount + vat, plan.Currency, now, plan.VatRate, vat, products.Count > 0 && products.All(item => item.BusinessSales)));
        return new SeatTerms(name.Length > 200 ? name[..200] : name, amount + vat, plan.Currency, daysLeft, documents, plan.VatRate, vat, plan.BuyerType);
    }

    /// <summary>The subscription and its account when the caller owns it: their own account, or an institution they administer.</summary>
    private async Task<(Subscription Subscription, Account Account)?> OwnedAsync(BillingUser user, Guid subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await db.Subscriptions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == subscriptionId, cancellationToken);
        if (subscription is null) return null;
        var account = await db.Accounts.AsNoTracking().SingleAsync(item => item.Id == subscription.AccountId, cancellationToken);
        if (account.Type == AccountType.User) return account.UserId == user.Id ? (subscription, account) : null;
        var administered = await institutions.GetAdministeredAsync(user.Id, cancellationToken);
        return administered.Any(item => item.Id == account.OrganizationId) ? (subscription, account) : null;
    }

    // ---- Product services ---------------------------------------------------------------------------------------------

    public async Task<SeatPoolView?> GetPoolAsync(SeatOwner owner, string productCode, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var pool = (await PoolsAsync(owner, productCode, now, cancellationToken)).FirstOrDefault();
        return pool is null ? null : await PoolAsync(pool, now, cancellationToken);
    }

    public async Task<SeatChangeResult> AssignAsync(SeatChangeRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var now = Now;
        var pools = await PoolsAsync(new SeatOwner(request.OwnerUserId, request.OrganizationId), request.Product, now, cancellationToken);
        if (pools.Count == 0) return new SeatChangeResult(false, "NoPool", null);
        var poolIds = pools.Select(item => item.Id).ToArray();
        // A user sits once in an owner's pools of a product: the seat they already have is the one that counts.
        var held = await db.Seats.AsNoTracking().Where(item => poolIds.Contains(item.SubscriptionId) && item.UserId == request.UserId && !item.IsSuspended)
            .Select(item => item.SubscriptionId).FirstOrDefaultAsync(cancellationToken);
        foreach (var candidate in held != Guid.Empty ? pools.Where(item => item.Id == held) : pools)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // The row lock makes counting and taking a seat one step: two requests cannot both take the last seat.
            var subscription = (await db.Subscriptions.FromSqlInterpolated($"SELECT * FROM billing.subscriptions WHERE \"Id\" = {candidate.Id} FOR UPDATE")
                .ToListAsync(cancellationToken)).Single();
            if (subscription.Status != SubscriptionStatus.Active || subscription.EndsAt <= now) continue;
            var seat = await db.Seats.SingleOrDefaultAsync(item => item.SubscriptionId == subscription.Id && item.UserId == request.UserId, cancellationToken);
            if (seat is { IsSuspended: false }) return new SeatChangeResult(true, "AlreadyAssigned", await PoolAsync(subscription, now, cancellationToken));
            var used = await db.Seats.CountAsync(item => item.SubscriptionId == subscription.Id && !item.IsSuspended, cancellationToken);
            if (used >= BillingRules.SeatCapacity(subscription.SeatCount, subscription.ExtraSeats, subscription.ExtraSeatsUntil, now)) continue;
            if (seat is null) db.Seats.Add(seat = new Seat { Id = Guid.NewGuid(), SubscriptionId = subscription.Id, UserId = request.UserId });
            seat.IsSuspended = false;
            seat.SuspensionReason = null;
            seat.AssignedBy = request.ActorId;
            // A seat given again goes to the end of the line: it does not push out somebody who kept theirs.
            seat.AssignedAt = now;
            seat.UpdatedAt = now;
            await orders.RebuildGrantsAsync(subscription, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new SeatChangeResult(true, "Assigned", await PoolAsync(subscription, now, cancellationToken));
        }
        return new SeatChangeResult(false, "PoolFull", await PoolAsync(pools[0], now, cancellationToken));
    }

    public async Task<SeatChangeResult> ReleaseAsync(SeatChangeRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var now = Now;
        var pools = await PoolsAsync(new SeatOwner(request.OwnerUserId, request.OrganizationId), request.Product, now, cancellationToken);
        var poolIds = pools.Select(item => item.Id).ToArray();
        var seat = await db.Seats.AsNoTracking().FirstOrDefaultAsync(item => poolIds.Contains(item.SubscriptionId) && item.UserId == request.UserId && !item.IsSuspended,
            cancellationToken);
        if (seat is null) return new SeatChangeResult(false, pools.Count == 0 ? "NoPool" : "NotAssigned", null);
        var pool = await SuspendAsync(seat.SubscriptionId, request.UserId, request.ActorId, request.Reason, now, cancellationToken);
        return new SeatChangeResult(true, "Released", pool);
    }

    // ---- Admin --------------------------------------------------------------------------------------------------------

    public async Task<SeatPoolView?> GetSeatsAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await db.Subscriptions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == subscriptionId, cancellationToken);
        return subscription is null ? null : await PoolAsync(subscription, Now, cancellationToken);
    }

    public async Task<SeatPoolView?> AdminReleaseAsync(Guid subscriptionId, Guid userId, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (!await db.Seats.AnyAsync(item => item.SubscriptionId == subscriptionId && item.UserId == userId && !item.IsSuspended, cancellationToken)) return null;
        return await SuspendAsync(subscriptionId, userId, actorId, "Yönetici koltuğu geri aldı.", Now, cancellationToken);
    }

    // ---- Shared -------------------------------------------------------------------------------------------------------

    private async Task<SeatPoolView> SuspendAsync(Guid subscriptionId, Guid userId, Guid actorId, string? reason, DateTime now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var subscription = (await db.Subscriptions.FromSqlInterpolated($"SELECT * FROM billing.subscriptions WHERE \"Id\" = {subscriptionId} FOR UPDATE")
            .ToListAsync(cancellationToken)).Single();
        var seat = await db.Seats.SingleOrDefaultAsync(item => item.SubscriptionId == subscriptionId && item.UserId == userId, cancellationToken);
        if (seat is { IsSuspended: false })
        {
            var text = reason?.Trim();
            seat.IsSuspended = true;
            seat.SuspensionReason = string.IsNullOrEmpty(text) ? "Koltuk geri alındı." : text.Length > 500 ? text[..500] : text;
            seat.AssignedBy = actorId;
            seat.UpdatedAt = now;
            await orders.RebuildGrantsAsync(subscription, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return await PoolAsync(subscription, now, cancellationToken);
    }

    /// <summary>
    /// The owner's running subscriptions that have seats and give a seat-side entitlement in the product, the one that
    /// ends last first.
    /// </summary>
    private async Task<List<Subscription>> PoolsAsync(SeatOwner owner, string productCode, DateTime now, CancellationToken cancellationToken)
    {
        var code = BillingRules.NormalizeCode(productCode);
        if (code is null || (owner.UserId is null) == (owner.OrganizationId is null)) return [];
        var account = owner.UserId is { } userId
            ? await db.Accounts.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            : await db.Accounts.AsNoTracking().SingleOrDefaultAsync(item => item.OrganizationId == owner.OrganizationId, cancellationToken);
        if (account is null) return [];
        var running = await db.Subscriptions.AsNoTracking().Where(item => item.AccountId == account.Id && item.Status == SubscriptionStatus.Active
            && item.StartsAt <= now && item.EndsAt > now && item.SeatCount != null).OrderByDescending(item => item.EndsAt).ToListAsync(cancellationToken);
        return running.Where(item => (JsonSerializer.Deserialize<OrderEntitlement[]>(item.EntitlementsJson, Json) ?? [])
            .Any(entitlement => entitlement.Holder == EntitlementHolder.SeatHolder && entitlement.ProductCode == code)).ToList();
    }

    private async Task<SeatPoolView> PoolAsync(Subscription subscription, DateTime now, CancellationToken cancellationToken)
    {
        var seats = await db.Seats.AsNoTracking().Where(item => item.SubscriptionId == subscription.Id)
            .OrderBy(item => item.IsSuspended).ThenBy(item => item.AssignedAt).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        var capacity = BillingRules.SeatCapacity(subscription.SeatCount, subscription.ExtraSeats, subscription.ExtraSeatsUntil, now);
        var running = subscription.Status == SubscriptionStatus.Active && subscription.EndsAt > now;
        var price = await db.Plans.AsNoTracking().Where(item => item.Id == subscription.PlanId).Select(item => item.ExtraSeatPrice).SingleOrDefaultAsync(cancellationToken);
        var position = 0;
        return new SeatPoolView(subscription.Id, subscription.PlanName, subscription.EndsAt, capacity, seats.Count(item => !item.IsSuspended),
            subscription.ExtraSeats > 0 && subscription.ExtraSeatsUntil > now ? subscription.ExtraSeats : 0, subscription.ExtraSeatsUntil, price,
            seats.Select(item => new SeatView(item.UserId, item.IsSuspended, item.AssignedAt, !item.IsSuspended && running && position++ < capacity)).ToArray());
    }

    private static SubscriptionView SubscriptionOf(Subscription subscription, DateTime now) =>
        new(subscription.Id, subscription.PlanId, subscription.PlanName,
            (JsonSerializer.Deserialize<OrderEntitlement[]>(subscription.EntitlementsJson, Json) ?? []).Select(item => item.ProductCode).Distinct().ToArray(),
            subscription.Status, subscription.StartsAt, subscription.EndsAt,
            subscription.Status == SubscriptionStatus.Active && subscription.StartsAt <= now && subscription.EndsAt > now, subscription.SeatCount);

    private static void Validate(SeatChangeRequest request)
    {
        if (request.UserId == Guid.Empty || request.ActorId == Guid.Empty || (request.OwnerUserId is null) == (request.OrganizationId is null))
            throw new BusinessRuleException("Billing.SeatRequestInvalid", "Koltuk isteği eksik: koltuk sahibi (kullanıcı ya da kurum), kullanıcı ve işlemi yapan gerekir.");
    }
}
