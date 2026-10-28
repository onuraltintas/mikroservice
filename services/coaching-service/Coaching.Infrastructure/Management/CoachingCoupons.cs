using System.Text.Json;
using System.Text.RegularExpressions;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Management;

public sealed record CoachingCouponQuote(string Code, decimal ListPrice, decimal Discount, decimal Amount);

/// <summary>Roadmap O10: how a discount code prices a plan for a buyer.</summary>
public static partial class CoachingCoupons
{
    public static string Normalize(string? code)
    {
        var value = (code ?? string.Empty).Trim().ToUpperInvariant();
        return CodePattern().IsMatch(value) ? value : throw new ArgumentException("İndirim kodu 3–30 karakter olmalı; yalnız harf, rakam ve tire içerebilir.");
    }

    public static IReadOnlyList<Guid> PlanIds(CoachingCoupon coupon) => JsonSerializer.Deserialize<Guid[]>(coupon.PlanIdsJson) ?? [];

    /// <summary>The price of <paramref name="plan"/> with <paramref name="code"/> for this buyer; throws with the reason when it cannot be used.</summary>
    public static async Task<(CoachingCoupon Coupon, CoachingCouponQuote Quote)> QuoteAsync(CoachingDbContext db, string? code, CoachingSubscriptionPlan plan,
        Guid userId, DateTime now, CancellationToken ct)
    {
        var normalized = Normalize(code);
        var coupon = await db.Set<CoachingCoupon>().AsNoTracking().SingleOrDefaultAsync(x => x.Code == normalized, ct);
        if (coupon is null || !coupon.IsActive) throw new ArgumentException("Bu indirim kodu geçerli değil.");
        if (coupon.ValidFrom > now || coupon.ValidUntil < now) throw new ArgumentException("Bu indirim kodunun geçerlilik süresi dışındasınız.");
        if (coupon.MaxUses is { } max && coupon.UsedCount >= max) throw new ArgumentException("Bu indirim kodunun kullanım sınırı doldu.");
        var plans = PlanIds(coupon);
        if (plans.Count > 0 && !plans.Contains(plan.Id)) throw new ArgumentException("Bu indirim kodu seçilen planda geçerli değil.");
        if (await db.CoachingBankTransferRequests.AsNoTracking().AnyAsync(x => x.UserId == userId && x.CouponCode == normalized
                && (x.Status == "Pending" || x.Status == "Approved"), ct))
            throw new ArgumentException("Bu indirim kodunu zaten kullandınız.");
        var discount = coupon.Kind == "Percent"
            ? Math.Round(plan.Price * coupon.Value / 100m, 2, MidpointRounding.AwayFromZero)
            : coupon.Value;
        var amount = plan.Price - discount;
        if (amount <= 0) throw new ArgumentException("Bu indirim kodu seçilen planda kullanılamaz.");
        return (coupon, new(coupon.Code, plan.Price, discount, amount));
    }

    [GeneratedRegex("^[A-Z0-9-]{3,30}$")]
    private static partial Regex CodePattern();
}
