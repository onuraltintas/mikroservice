using System.Text.Json;
using Billing.Application;
using Billing.Domain;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure;

/// <summary>Coupons, free trials, withdrawal (cayma) and invoices, with the rules carried over from Koçluk.</summary>
public sealed class BillingExtras(BillingDbContext db, BillingOrders orders, TimeProvider clock) : IBillingExtras
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---- Coupons ------------------------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<CouponView>> GetCouponsAsync(CancellationToken cancellationToken = default) =>
        await ViewsAsync(await db.Coupons.AsNoTracking().OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken), cancellationToken);

    public async Task<CouponView> CreateCouponAsync(CouponRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var code = ExtrasRules.NormalizeCouponCode(request.Code)
            ?? throw new BusinessRuleException("Billing.CouponInvalid", "Kupon kodu 3–30 karakter olmalı; harf, rakam, tire ve alt çizgi içerebilir.");
        if (await db.Coupons.AnyAsync(item => item.Code == code, cancellationToken))
            throw new BusinessRuleException("Billing.CouponExists", "Bu kupon kodu zaten var.");
        var coupon = new Coupon { Id = Guid.NewGuid(), Code = code, CreatedBy = actorId, CreatedAt = Now };
        await ApplyAsync(coupon, request, cancellationToken);
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync(cancellationToken);
        return (await ViewsAsync([coupon], cancellationToken))[0];
    }

    /// <summary>The code is what orders carry; it never changes.</summary>
    public async Task<CouponView?> UpdateCouponAsync(Guid id, CouponRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var coupon = await db.Coupons.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (coupon is null) return null;
        await ApplyAsync(coupon, request, cancellationToken);
        coupon.UpdatedBy = actorId;
        coupon.UpdatedAt = Now;
        await db.SaveChangesAsync(cancellationToken);
        return (await ViewsAsync([coupon], cancellationToken))[0];
    }

    private async Task ApplyAsync(Coupon coupon, CouponRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Kind)) throw new BusinessRuleException("Billing.CouponInvalid", "İndirim türü geçersiz.");
        var startsAt = request.StartsAt?.ToUniversalTime();
        var endsAt = request.EndsAt?.ToUniversalTime();
        if (ExtrasRules.CouponProblem(request.Kind, request.Value, startsAt, endsAt, request.MaxUses) is { } problem)
            throw new BusinessRuleException("Billing.CouponInvalid", problem);
        var planIds = (request.PlanIds ?? []).Distinct().ToArray();
        if (planIds.Length > 0 && await db.Plans.CountAsync(item => planIds.Contains(item.Id), cancellationToken) != planIds.Length)
            throw new BusinessRuleException("Billing.CouponInvalid", "Seçilen planlardan biri bulunamadı.");
        coupon.Kind = request.Kind;
        coupon.Value = Math.Round(request.Value, 2, MidpointRounding.AwayFromZero);
        coupon.StartsAt = startsAt;
        coupon.EndsAt = endsAt;
        coupon.MaxUses = request.MaxUses;
        coupon.PlanIdsJson = JsonSerializer.Serialize(planIds, Json);
        coupon.IsActive = request.IsActive;
    }

    public async Task<CouponQuote> QuoteCouponAsync(BillingUser user, CouponQuoteRequest request, CancellationToken cancellationToken = default)
    {
        var plan = await db.Plans.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.PlanId && item.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("Billing.PlanNotFound", "Plan bulunamadı.");
        var accountId = await db.Accounts.AsNoTracking().Where(item => item.UserId == user.Id).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
        var (discount, code) = await orders.CouponAsync(request.Code, plan, accountId, Now, false, cancellationToken);
        return code is null
            ? throw new BusinessRuleException("Billing.CouponInvalid", "Kupon kodunu yazın.")
            : new CouponQuote(code, plan.Price, discount, plan.Price - discount, plan.Currency);
    }

    private async Task<IReadOnlyList<CouponView>> ViewsAsync(IReadOnlyList<Coupon> coupons, CancellationToken cancellationToken)
    {
        var codes = coupons.Select(item => item.Code).ToArray();
        var used = await db.Orders.AsNoTracking().Where(item => item.CouponCode != null && codes.Contains(item.CouponCode)
                && (item.Status == OrderStatus.Pending || item.Status == OrderStatus.Paid))
            .GroupBy(item => item.CouponCode!).Select(group => new { Code = group.Key, Count = group.Count() }).ToDictionaryAsync(item => item.Code, item => item.Count, cancellationToken);
        return coupons.Select(item => new CouponView(item.Id, item.Code, item.Kind, item.Value, item.StartsAt, item.EndsAt, item.MaxUses,
            used.GetValueOrDefault(item.Code), JsonSerializer.Deserialize<Guid[]>(item.PlanIdsJson, Json) ?? [], item.IsActive)).ToArray();
    }

    // ---- Free trial ---------------------------------------------------------------------------------------------------

    public async Task<SubscriptionView> StartTrialAsync(BillingUser user, StartTrialRequest request, CancellationToken cancellationToken = default)
    {
        var plan = await db.Plans.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.PlanId, cancellationToken)
            ?? throw new BusinessRuleException("Billing.PlanNotFound", "Plan bulunamadı.");
        var (entitlements, onSale) = await orders.PlanEntitlementsAsync(plan.Id, cancellationToken);
        if (!plan.IsActive || plan.TrialDays <= 0 || plan.BuyerType == BuyerType.Institution || !onSale)
            throw new BusinessRuleException("Billing.TrialUnavailable", "Bu planda ücretsiz deneme yok.");
        if (plan.BuyerType == BuyerType.Teacher && !user.Roles.Contains("Teacher"))
            throw new BusinessRuleException("Billing.PlanNotForBuyer", "Bu planı yalnız öğretmenler kullanabilir.");

        // A trial is for somebody who never had the product: no subscription of any kind, paid or trial, in the plan's products.
        var codes = entitlements.Select(item => item.ProductCode).Distinct().ToArray();
        var earlier = await db.Subscriptions.AsNoTracking().Where(item => item.BeneficiaryUserId == user.Id).Select(item => item.EntitlementsJson).ToListAsync(cancellationToken);
        if (earlier.Any(json => (JsonSerializer.Deserialize<OrderEntitlement[]>(json, Json) ?? []).Any(item => codes.Contains(item.ProductCode))))
            throw new BusinessRuleException("Billing.TrialUsed", "Ücretsiz deneme yalnız bu ürünü daha önce kullanmamış hesaplar içindir.");

        var now = Now;
        var name = request.BuyerName?.Trim() is { Length: >= 3 and <= 200 } given ? given : user.Email ?? "Deneme kullanıcısı";
        var (account, beneficiary) = await orders.AccountAsync(user, null, name, now, cancellationToken);
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(), AccountId = account.Id, PlanId = plan.Id, BeneficiaryUserId = beneficiary, PlanName = plan.Name,
            Status = SubscriptionStatus.Active, StartsAt = now, EndsAt = now.AddDays(plan.TrialDays), SeatCount = plan.SeatCount,
            EntitlementsJson = JsonSerializer.Serialize(entitlements, Json), CreatedAt = now
        };
        db.Subscriptions.Add(subscription);
        db.SubscriptionPeriods.Add(new SubscriptionPeriod { Id = Guid.NewGuid(), SubscriptionId = subscription.Id, Source = PeriodSource.Trial,
            Status = PeriodStatus.Active, StartsAt = subscription.StartsAt, EndsAt = subscription.EndsAt, CreatedAt = now });
        await orders.RebuildGrantsAsync(subscription, now, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            throw new BusinessRuleException("Billing.TrialUsed", "Ücretsiz deneme yalnız bu ürünü daha önce kullanmamış hesaplar içindir.");
        }
        return new SubscriptionView(subscription.Id, plan.Id, plan.Name, codes, subscription.Status, subscription.StartsAt, subscription.EndsAt, true, subscription.SeatCount);
    }

    // ---- Withdrawal ---------------------------------------------------------------------------------------------------

    public async Task<OrderView?> RequestWithdrawalAsync(Guid userId, Guid orderId, string? reason, CancellationToken cancellationToken = default)
    {
        var text = reason?.Trim();
        if (text is { Length: > 1_000 }) throw new BusinessRuleException("Billing.WithdrawalInvalid", "Açıklama en fazla 1000 karakter olabilir.");
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == orderId && item.OrderedByUserId == userId, cancellationToken);
        if (order is null) return null;
        var now = Now;
        if (order.Status != OrderStatus.Paid || order.ReviewedAt is not { } approvedAt)
            throw new BusinessRuleException("Billing.WithdrawalInvalid", "Cayma hakkı yalnız onaylanmış bir ödeme için kullanılabilir.");
        if (ExtrasRules.WithdrawalDeadline(approvedAt) <= now)
            throw new BusinessRuleException("Billing.WithdrawalExpired", "Cayma süresi (ödemenin onayından sonra 14 gün) doldu.");
        if (await db.Withdrawals.AnyAsync(item => item.OrderId == order.Id && item.Status != WithdrawalStatus.Rejected, cancellationToken))
            throw new BusinessRuleException("Billing.WithdrawalExists", "Bu ödeme için cayma talebiniz zaten var.");
        db.Withdrawals.Add(new Withdrawal { Id = Guid.NewGuid(), OrderId = order.Id, Status = WithdrawalStatus.Requested,
            Reason = string.IsNullOrEmpty(text) ? null : text, RequestedBy = userId, RequestedAt = now });
        await orders.NoticeAsync(EduPlatform.Shared.Contracts.Events.Billing.BillingNoticeKinds.WithdrawalRequested, order, null,
            $"{(await db.Accounts.AsNoTracking().SingleAsync(item => item.Id == order.AccountId, cancellationToken)).DisplayName} · {order.PaymentCode}", cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            throw new BusinessRuleException("Billing.WithdrawalExists", "Bu ödeme için cayma talebiniz zaten var.");
        }
        return await orders.ViewAsync(order, cancellationToken);
    }

    public async Task<IReadOnlyList<WithdrawalView>> GetWithdrawalsAsync(WithdrawalStatus? status, CancellationToken cancellationToken = default)
    {
        var query = db.Withdrawals.AsNoTracking();
        if (status is { } wanted) query = query.Where(item => item.Status == wanted);
        return await ViewsAsync(await query.OrderByDescending(item => item.RequestedAt).Take(BillingOrders.MaxListedOrders).ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<WithdrawalView?> ReviewWithdrawalAsync(Guid id, ReviewWithdrawalRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var note = request.Note?.Trim();
        var withdrawal = await db.Withdrawals.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (withdrawal is null) return null;
        if (withdrawal.Status != WithdrawalStatus.Requested)
            throw new BusinessRuleException("Billing.WithdrawalReviewed", "Bu cayma talebi daha önce sonuçlandı.");
        if (request.Approve)
        {
            // Accepting ends the access and reverses the payment in one transaction; the reversal settles this request.
            await orders.ReverseApprovalAsync(withdrawal.OrderId, actorId,
                string.IsNullOrEmpty(note) ? "Cayma talebi onaylandı; ödeme, ödemenin yapıldığı hesaba iade edilecek." : note, cancellationToken);
            await db.Entry(withdrawal).ReloadAsync(cancellationToken);
        }
        else
        {
            if (note is not { Length: >= 5 and <= 1_000 })
                throw new BusinessRuleException("Billing.NoteRequired", "Ret gerekçesi yazın (5–1000 karakter).");
            withdrawal.Status = WithdrawalStatus.Rejected;
            withdrawal.ReviewNote = note;
            withdrawal.ReviewedBy = actorId;
            withdrawal.ReviewedAt = Now;
            var refused = await db.Orders.AsNoTracking().SingleAsync(item => item.Id == withdrawal.OrderId, cancellationToken);
            await orders.NoticeAsync(EduPlatform.Shared.Contracts.Events.Billing.BillingNoticeKinds.WithdrawalRejected, refused, null, note, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        return (await ViewsAsync([withdrawal], cancellationToken))[0];
    }

    private async Task<IReadOnlyList<WithdrawalView>> ViewsAsync(IReadOnlyList<Withdrawal> withdrawals, CancellationToken cancellationToken)
    {
        var orderIds = withdrawals.Select(item => item.OrderId).Distinct().ToArray();
        var rows = await (from order in db.Orders.AsNoTracking()
                          join account in db.Accounts.AsNoTracking() on order.AccountId equals account.Id
                          where orderIds.Contains(order.Id)
                          select new { order.Id, account.DisplayName, account.Email, order.PlanName, order.PaymentCode, order.Amount, order.Currency })
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        return withdrawals.Select(item =>
        {
            var row = rows[item.OrderId];
            return new WithdrawalView(item.Id, item.OrderId, item.Status, item.Reason, item.RequestedAt, item.ReviewNote, item.ReviewedAt,
                row.DisplayName, row.Email, row.PlanName, row.PaymentCode, row.Amount, row.Currency);
        }).ToArray();
    }

    // ---- Invoices -----------------------------------------------------------------------------------------------------

    public async Task<AdminOrderView?> AttachInvoiceAsync(Guid orderId, InvoiceUpload upload, Guid actorId, CancellationToken cancellationToken = default)
    {
        var number = ExtrasRules.NormalizeInvoiceNumber(upload.Number)
            ?? throw new BusinessRuleException("Billing.InvoiceInvalid", "Fatura numarası 3–40 karakter olmalıdır.");
        if (upload.Content.Length is 0 or > ExtrasRules.MaxInvoiceBytes || !ExtrasRules.IsPdf(upload.Content))
            throw new BusinessRuleException("Billing.InvoiceInvalid", "Fatura PDF olmalı ve en fazla 10 MB olabilir.");
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);
        if (order is null) return null;
        var payment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(item => item.OrderId == orderId && item.Status == PaymentStatus.Approved, cancellationToken)
            ?? throw new BusinessRuleException("Billing.InvoiceInvalid", "Fatura yalnız onaylanmış ödemeye eklenir.");
        if (await db.Invoices.AnyAsync(item => item.PaymentId == payment.Id, cancellationToken))
            throw new BusinessRuleException("Billing.InvoiceExists", "Bu ödemenin faturası var; değiştirmek için önce kaldırın.");
        if (await db.Invoices.AnyAsync(item => item.Number == number, cancellationToken))
            throw new BusinessRuleException("Billing.InvoiceExists", "Bu fatura numarası başka bir ödemede kullanıldı.");
        db.Invoices.Add(new Invoice { Id = Guid.NewGuid(), PaymentId = payment.Id, OrderId = order.Id, Number = number, IssuedOn = upload.IssuedOn,
            FileName = SafeName(upload.FileName, number), Content = upload.Content, Size = upload.Content.Length, UploadedBy = actorId, UploadedAt = Now });
        await orders.NoticeAsync(EduPlatform.Shared.Contracts.Events.Billing.BillingNoticeKinds.InvoiceReady, order, null, number, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            throw new BusinessRuleException("Billing.InvoiceExists", "Bu ödemenin faturası ya da bu fatura numarası zaten kayıtlı.");
        }
        return await orders.AdminViewAsync(order, cancellationToken);
    }

    public async Task<bool> RemoveInvoiceAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        await db.Invoices.Where(item => item.OrderId == orderId).ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task<InvoiceFile?> GetInvoiceAsync(Guid? userId, Guid orderId, CancellationToken cancellationToken = default)
    {
        if (userId is { } owner && !await db.Orders.AnyAsync(item => item.Id == orderId
            && (item.OrderedByUserId == owner || item.BeneficiaryUserId == owner), cancellationToken)) return null;
        var invoice = await db.Invoices.AsNoTracking().SingleOrDefaultAsync(item => item.OrderId == orderId, cancellationToken);
        return invoice is null ? null : new InvoiceFile(invoice.Number, invoice.FileName, invoice.Content);
    }

    private static string SafeName(string? name, string number)
    {
        var raw = name ?? string.Empty;
        var cut = raw.LastIndexOfAny(['/', '\\']);
        var file = new string((cut >= 0 ? raw[(cut + 1)..] : raw).Where(character => !char.IsControl(character) && character != '"').ToArray()).Trim();
        if (file.Length > 150) file = file[^150..];
        return file.Length > 0 && file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? file
            : $"fatura-{new string(number.Where(char.IsLetterOrDigit).ToArray())}.pdf";
    }
}
