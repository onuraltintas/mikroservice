using System.Text.Json;
using Billing.Application;
using Billing.Domain;
using EduPlatform.Shared.Contracts.Events.Billing;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure;

/// <summary>
/// What time does to billing records: reminders before and when a subscription ends, and the retention of an erased
/// account's financial records (kept ten years, then stripped of what names the person).
/// </summary>
public sealed class BillingLifecycle(BillingDbContext db, BillingOrders orders, IBillingEvents events, TimeProvider clock) : IBillingLifecycle
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---- Reminders ----------------------------------------------------------------------------------------------------

    public async Task<int> SendRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = Now;
        var soon = now.AddDays(RetentionRules.ReminderDays);
        var recentlyOver = now.AddDays(-3);
        var due = await (from subscription in db.Subscriptions.AsNoTracking()
                         join account in db.Accounts.AsNoTracking() on subscription.AccountId equals account.Id
                         where subscription.Status == SubscriptionStatus.Active && subscription.EndsAt <= soon && subscription.EndsAt > recentlyOver
                             && account.Email != null
                         orderby subscription.EndsAt
                         select new { subscription, account }).Take(500).ToListAsync(cancellationToken);
        if (due.Count == 0) return 0;
        // A product that asks for no subscription sells nothing: nobody is urged to renew there.
        var selling = (await db.Products.AsNoTracking().Where(item => item.IsActive && item.RequireSubscription).Select(item => item.Code).ToListAsync(cancellationToken)).ToHashSet();
        var ids = due.Select(item => item.subscription.Id).ToArray();
        var sent = (await db.NoticeReceipts.AsNoTracking().Where(item => ids.Contains(item.SubscriptionId)).ToListAsync(cancellationToken))
            .Select(item => (item.SubscriptionId, item.Kind, item.PeriodEnd)).ToHashSet();
        var count = 0;
        foreach (var item in due)
        {
            var subscription = item.subscription;
            var codes = (JsonSerializer.Deserialize<OrderEntitlement[]>(subscription.EntitlementsJson, Json) ?? []).Select(entitlement => entitlement.ProductCode).Distinct().ToArray();
            if (!codes.Any(selling.Contains)) continue;
            var kind = subscription.EndsAt <= now ? BillingNoticeKinds.Expired
                : subscription.EndsAt <= now.AddDays(1) ? BillingNoticeKinds.ExpiringTomorrow : BillingNoticeKinds.Expiring;
            if (!sent.Add((subscription.Id, kind, subscription.EndsAt))) continue;
            db.NoticeReceipts.Add(new NoticeReceipt { Id = Guid.NewGuid(), SubscriptionId = subscription.Id, Kind = kind, PeriodEnd = subscription.EndsAt, SentAt = now });
            await events.PublishAsync(new BillingNoticeV1(Guid.NewGuid(), item.account.UserId, item.account.Email!.Trim(), kind, codes, subscription.PlanName,
                subscription.EndsAt, subscription.SeatCount is null ? null : "Seats", null, null, null, now), cancellationToken);
            count++;
        }
        if (count > 0) await db.SaveChangesAsync(cancellationToken);
        return count;
    }

    // ---- Erased accounts ----------------------------------------------------------------------------------------------

    public async Task<ErasureAssessment> AssessErasureAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var orderIds = await db.Orders.AsNoTracking().Where(item => item.OrderedByUserId == userId || item.BeneficiaryUserId == userId).Select(item => item.Id).ToListAsync(cancellationToken);
        return new ErasureAssessment(orderIds.Count,
            await db.Payments.CountAsync(item => orderIds.Contains(item.OrderId), cancellationToken),
            await db.Subscriptions.CountAsync(item => item.BeneficiaryUserId == userId, cancellationToken),
            await db.Seats.CountAsync(item => item.UserId == userId, cancellationToken),
            await db.Invoices.CountAsync(item => orderIds.Contains(item.OrderId), cancellationToken));
    }

    public async Task<int> EraseUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (await db.ErasureReceipts.AnyAsync(item => item.UserId == userId, cancellationToken)) return 0;
        var now = Now;
        var changed = 0;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.ErasureReceipts.Add(new ErasureReceipt { UserId = userId, ErasedAt = now, RedactAfter = now.AddYears(RetentionRules.FinancialRetentionYears) });

        // Waiting orders of the account are closed; nothing can be approved for it any more.
        var waiting = await db.Orders.Where(item => (item.OrderedByUserId == userId || item.BeneficiaryUserId == userId) && item.Status == OrderStatus.Pending).ToListAsync(cancellationToken);
        foreach (var order in waiting)
        {
            order.Status = OrderStatus.Withdrawn;
            order.ReviewNote = "Hesap silindi.";
            order.UpdatedAt = now;
            changed++;
        }
        // A receipt (dekont) is not a financial record of the seller; it goes with the account.
        var ownOrderIds = await db.Orders.Where(item => item.OrderedByUserId == userId).Select(item => item.Id).ToListAsync(cancellationToken);
        changed += await db.OrderReceipts.Where(item => ownOrderIds.Contains(item.OrderId)).ExecuteDeleteAsync(cancellationToken);

        // The account's own access ends now; what was paid stays on record.
        var subscriptions = await db.Subscriptions.Where(item => item.BeneficiaryUserId == userId && item.Status == SubscriptionStatus.Active).ToListAsync(cancellationToken);
        foreach (var subscription in subscriptions)
        {
            subscription.Status = SubscriptionStatus.Cancelled;
            if (subscription.EndsAt > now) subscription.EndsAt = subscription.StartsAt > now ? subscription.StartsAt : now;
            subscription.UpdatedAt = now;
            await orders.RebuildGrantsAsync(subscription, now, cancellationToken);
            changed++;
        }
        // Seats the account held in somebody else's pool are given up.
        var seats = await db.Seats.Where(item => item.UserId == userId && !item.IsSuspended).ToListAsync(cancellationToken);
        foreach (var seat in seats)
        {
            seat.IsSuspended = true;
            seat.SuspensionReason = "Hesap silindi.";
            seat.UpdatedAt = now;
            changed++;
        }
        var poolIds = seats.Select(seat => seat.SubscriptionId).Distinct().ToArray();
        foreach (var pool in await db.Subscriptions.Where(item => poolIds.Contains(item.Id)).ToListAsync(cancellationToken))
            await orders.RebuildGrantsAsync(pool, now, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return changed;
    }

    public async Task<int> RedactExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = Now;
        var due = await db.ErasureReceipts.Where(item => item.RedactedAt == null && item.RedactAfter <= now).OrderBy(item => item.RedactAfter).Take(50).ToListAsync(cancellationToken);
        foreach (var receipt in due)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var account = await db.Accounts.SingleOrDefaultAsync(item => item.UserId == receipt.UserId, cancellationToken);
            if (account is not null)
            {
                account.DisplayName = RetentionRules.RedactedName;
                account.Email = null;
                account.UpdatedAt = now;
            }
            var redactedDocuments = JsonSerializer.Serialize(new SalesDocuments("-", RetentionRules.RedactedText, RetentionRules.RedactedText), Json);
            var ownOrders = await db.Orders.Where(item => item.OrderedByUserId == receipt.UserId || item.BeneficiaryUserId == receipt.UserId).ToListAsync(cancellationToken);
            foreach (var order in ownOrders)
            {
                // The contract names the buyer; the amounts, dates and the payment code stay as the bookkeeping record.
                order.DocumentsJson = redactedDocuments;
                order.PayerNote = null;
                order.ReviewNote = null;
                order.UpdatedAt = now;
            }
            var orderIds = ownOrders.Select(item => item.Id).ToArray();
            // A bank line names its sender; an invoice names its buyer.
            await db.BankTransactions.Where(item => item.OrderId != null && orderIds.Contains(item.OrderId.Value))
                .ExecuteUpdateAsync(set => set.SetProperty(item => item.Sender, (string?)null).SetProperty(item => item.Description, RetentionRules.RedactedText)
                    .SetProperty(item => item.Note, (string?)null), cancellationToken);
            await db.Invoices.Where(item => orderIds.Contains(item.OrderId)).ExecuteDeleteAsync(cancellationToken);
            await db.Withdrawals.Where(item => orderIds.Contains(item.OrderId))
                .ExecuteUpdateAsync(set => set.SetProperty(item => item.Reason, (string?)null).SetProperty(item => item.ReviewNote, (string?)null), cancellationToken);
            receipt.RedactedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return due.Count;
    }

    public async Task<int> ClearUnmatchedBankLinesAsync(CancellationToken cancellationToken = default)
    {
        var before = Now.AddYears(-RetentionRules.UnmatchedBankLineYears);
        // The fingerprint stays, so the same line uploaded again is still recognised and not added twice.
        return await db.BankTransactions
            .Where(item => item.OrderId == null && item.BookedAt <= before
                && (item.Sender != null || item.Note != null || item.Description != RetentionRules.RedactedText))
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.Sender, (string?)null)
                .SetProperty(item => item.Description, RetentionRules.RedactedText)
                .SetProperty(item => item.Note, (string?)null), cancellationToken);
    }
}
