using System.Text.Json;
using Billing.Application;
using Billing.Domain;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure;

/// <summary>The admin's overview and the subscriptions given, extended and ended by hand (stage 6 of billing-service-plan.md).</summary>
public sealed class BillingAdministration(BillingDbContext db, BillingOrders orders, TimeProvider clock) : IBillingAdministration
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    /// <summary>Months are the seller's: Turkey keeps UTC+3 all year.</summary>
    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<BillingOverview> GetOverviewAsync(string? productCode, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var code = BillingRules.NormalizeCode(productCode);
        bool Fits(string entitlementsJson) => code is null || Codes(entitlementsJson).Contains(code);

        var waiting = (await db.Orders.AsNoTracking().Where(item => item.Status == OrderStatus.Pending && item.ExpiresAt > now)
            .Select(item => new { item.EntitlementsJson, item.PaidDeclaredAt }).ToListAsync(cancellationToken)).Where(item => Fits(item.EntitlementsJson)).ToArray();
        var withdrawals = (await (from withdrawal in db.Withdrawals.AsNoTracking()
                                  join order in db.Orders.AsNoTracking() on withdrawal.OrderId equals order.Id
                                  where withdrawal.Status == WithdrawalStatus.Requested
                                  select order.EntitlementsJson).ToListAsync(cancellationToken)).Count(Fits);
        // A bank line belongs to no product until it is tied to an order.
        var unmatched = await db.BankTransactions.AsNoTracking().CountAsync(item => item.Status == BankTransactionStatus.Unmatched, cancellationToken);

        var local = now + TurkeyOffset;
        var thisMonth = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Utc) - TurkeyOffset;
        var lastMonth = thisMonth.AddMonths(-1);
        var payments = (await (from payment in db.Payments.AsNoTracking()
                               join order in db.Orders.AsNoTracking() on payment.OrderId equals order.Id
                               where payment.Status == PaymentStatus.Approved && payment.ApprovedAt >= lastMonth
                               select new { payment.Amount, payment.ApprovedAt, order.EntitlementsJson }).ToListAsync(cancellationToken))
            .Where(item => Fits(item.EntitlementsJson)).ToArray();

        var rows = await (from subscription in db.Subscriptions.AsNoTracking()
                          join account in db.Accounts.AsNoTracking() on subscription.AccountId equals account.Id
                          where subscription.Status == SubscriptionStatus.Active && subscription.StartsAt <= now && subscription.EndsAt > now
                          orderby subscription.EndsAt
                          select new { subscription, account.DisplayName, account.Email }).ToListAsync(cancellationToken);
        var current = rows.Where(row => Fits(row.subscription.EntitlementsJson)).ToArray();
        var soon = now.AddDays(BillingRules.EndingSoonDays);
        return new BillingOverview(waiting.Length, waiting.Count(item => item.PaidDeclaredAt is not null), withdrawals, unmatched, current.Length,
            payments.Where(item => item.ApprovedAt >= thisMonth).Sum(item => item.Amount),
            payments.Where(item => item.ApprovedAt < thisMonth).Sum(item => item.Amount), "TRY",
            current.Where(row => row.subscription.EndsAt <= soon).Take(50)
                .Select(row => View(row.subscription, row.DisplayName, row.Email, now)).ToArray());
    }

    public async Task<AdminSubscriptionView> GrantAsync(GrantSubscriptionRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (request.UserId is null == request.OrganizationId is null)
            throw new BusinessRuleException("Billing.GrantInvalid", "Aboneliğin kullanıcıya mı kuruma mı verileceğini seçin.");
        var name = request.DisplayName?.Trim();
        if (name is not { Length: >= 3 and <= 200 }) throw new BusinessRuleException("Billing.GrantInvalid", "Ad 3-200 karakter olmalıdır.");
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (email is { Length: > 256 } || (email is not null && !email.Contains('@')))
            throw new BusinessRuleException("Billing.GrantInvalid", "E-posta adresi geçersiz.");
        var note = RequireNote(request.Note);
        var plan = await db.Plans.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.PlanId, cancellationToken)
            ?? throw new BusinessRuleException("Billing.PlanNotFound", "Plan bulunamadı.");
        if (plan.BuyerType == BuyerType.Institution != request.OrganizationId is not null)
            throw new BusinessRuleException("Billing.PlanNotForBuyer", plan.BuyerType == BuyerType.Institution
                ? "Kurum planı yalnız bir kuruma verilebilir." : "Bu plan bir kuruma verilemez.");
        var days = Days(request.Days ?? plan.DurationDays);
        var (entitlements, _) = await orders.PlanEntitlementsAsync(plan.Id, cancellationToken);
        if (entitlements.Length == 0) throw new BusinessRuleException("Billing.PlanHasNoEntitlement", "Bu plan hiçbir hak vermiyor.");
        if (request.UserId is { } erased && await db.ErasureReceipts.AnyAsync(item => item.UserId == erased, cancellationToken))
            throw new BusinessRuleException("Billing.UserErased", "Bu kullanıcının hesabı silindi; abonelik verilemez.");

        var now = Now;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var account = request.UserId is { } userId
            ? await db.Accounts.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            : await db.Accounts.SingleOrDefaultAsync(item => item.OrganizationId == request.OrganizationId, cancellationToken);
        if (account is null)
        {
            account = new Account { Id = Guid.NewGuid(), Type = request.UserId is null ? AccountType.Organization : AccountType.User,
                UserId = request.UserId, OrganizationId = request.OrganizationId, DisplayName = name, Email = email, CreatedAt = now };
            db.Accounts.Add(account);
            // The account row goes in first: the subscription is looked up under a lock by its id.
            await db.SaveChangesAsync(cancellationToken);
        }
        var beneficiary = request.UserId ?? request.OrganizationId!.Value;
        var subscription = (await db.Subscriptions.FromSqlInterpolated(
                $"SELECT * FROM billing.subscriptions WHERE \"AccountId\" = {account.Id} AND \"PlanId\" = {plan.Id} AND \"BeneficiaryUserId\" = {beneficiary} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault();
        var running = subscription is { Status: SubscriptionStatus.Active } && subscription.EndsAt > now;
        var (startsAt, endsAt) = BillingRules.NextPeriod(now, running ? subscription!.EndsAt : null, days);
        if (subscription is null)
        {
            subscription = new Subscription { Id = Guid.NewGuid(), AccountId = account.Id, PlanId = plan.Id, BeneficiaryUserId = beneficiary, CreatedAt = now };
            db.Subscriptions.Add(subscription);
        }
        if (!running) subscription.StartsAt = startsAt;
        subscription.Status = SubscriptionStatus.Active;
        subscription.EndsAt = endsAt;
        subscription.PlanName = plan.Name;
        subscription.SeatCount = plan.SeatCount;
        subscription.EntitlementsJson = JsonSerializer.Serialize(entitlements, Json);
        subscription.UpdatedAt = now;
        db.SubscriptionPeriods.Add(Manual(subscription.Id, startsAt, endsAt, now, actorId, note));
        await orders.RebuildGrantsAsync(subscription, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return View(subscription, account.DisplayName, account.Email, now);
    }

    public async Task<AdminSubscriptionView?> ExtendAsync(Guid subscriptionId, ExtendSubscriptionRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var days = Days(request.Days);
        var note = RequireNote(request.Note);
        var now = Now;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var subscription = await LockAsync(subscriptionId, cancellationToken);
        if (subscription is null) return null;
        if (await db.ErasureReceipts.AnyAsync(item => item.UserId == subscription.BeneficiaryUserId, cancellationToken))
            throw new BusinessRuleException("Billing.UserErased", "Bu kullanıcının hesabı silindi; abonelik uzatılamaz.");
        var running = subscription.Status == SubscriptionStatus.Active && subscription.EndsAt > now;
        var (startsAt, endsAt) = BillingRules.NextPeriod(now, running ? subscription.EndsAt : null, days);
        if (!running) subscription.StartsAt = startsAt;
        subscription.Status = SubscriptionStatus.Active;
        subscription.EndsAt = endsAt;
        subscription.UpdatedAt = now;
        db.SubscriptionPeriods.Add(Manual(subscription.Id, startsAt, endsAt, now, actorId, note));
        await orders.RebuildGrantsAsync(subscription, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ViewAsync(subscription, now, cancellationToken);
    }

    public async Task<AdminSubscriptionView?> CancelAsync(Guid subscriptionId, CancelSubscriptionRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var note = RequireNote(request.Note);
        var now = Now;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var subscription = await LockAsync(subscriptionId, cancellationToken);
        if (subscription is null) return null;
        if (subscription.Status != SubscriptionStatus.Active || subscription.EndsAt <= now)
            throw new BusinessRuleException("Billing.SubscriptionNotRunning", "Bu abonelik zaten sona ermiş.");
        foreach (var period in await db.SubscriptionPeriods.Where(item => item.SubscriptionId == subscription.Id && item.Status == PeriodStatus.Active
            && item.EndsAt > now).ToListAsync(cancellationToken))
        {
            // What was used stays on record; what was left is cut off.
            period.EndsAt = period.StartsAt > now ? period.StartsAt : now;
            period.Status = PeriodStatus.Cancelled;
            period.CancelledAt = now;
            period.ActorId = actorId;
            period.Note = note;
        }
        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.EndsAt = now < subscription.StartsAt ? subscription.StartsAt : now;
        subscription.UpdatedAt = now;
        await orders.RebuildGrantsAsync(subscription, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ViewAsync(subscription, now, cancellationToken);
    }

    private async Task<Subscription?> LockAsync(Guid id, CancellationToken cancellationToken) =>
        (await db.Subscriptions.FromSqlInterpolated($"SELECT * FROM billing.subscriptions WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(cancellationToken)).SingleOrDefault();

    private async Task<AdminSubscriptionView> ViewAsync(Subscription subscription, DateTime now, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.AsNoTracking().SingleAsync(item => item.Id == subscription.AccountId, cancellationToken);
        return View(subscription, account.DisplayName, account.Email, now);
    }

    private static SubscriptionPeriod Manual(Guid subscriptionId, DateTime startsAt, DateTime endsAt, DateTime now, Guid actorId, string note) => new()
    {
        Id = Guid.NewGuid(), SubscriptionId = subscriptionId, Source = PeriodSource.Manual, Status = PeriodStatus.Active,
        StartsAt = startsAt, EndsAt = endsAt, CreatedAt = now, ActorId = actorId, Note = note
    };

    private static int Days(int days) => days is >= 1 and <= BillingRules.MaxManualDays
        ? days
        : throw new BusinessRuleException("Billing.GrantInvalid", $"Süre 1-{BillingRules.MaxManualDays} gün olmalıdır.");

    private static string RequireNote(string? value) => value?.Trim() is { Length: >= 3 and <= 500 } note
        ? note
        : throw new BusinessRuleException("Billing.NoteRequired", "Gerekçe 3-500 karakter olmalıdır.");

    private static string[] Codes(string entitlementsJson) =>
        (JsonSerializer.Deserialize<OrderEntitlement[]>(entitlementsJson, Json) ?? []).Select(item => item.ProductCode).Distinct().ToArray();

    private static AdminSubscriptionView View(Subscription subscription, string accountName, string? accountEmail, DateTime now) =>
        new(new SubscriptionView(subscription.Id, subscription.PlanId, subscription.PlanName, Codes(subscription.EntitlementsJson), subscription.Status,
            subscription.StartsAt, subscription.EndsAt, subscription.Status == SubscriptionStatus.Active && subscription.StartsAt <= now && subscription.EndsAt > now,
            subscription.SeatCount), accountName, accountEmail, subscription.BeneficiaryUserId);
}
