namespace Billing.Domain;

public enum CouponKind
{
    Percent = 0,
    Amount = 1
}

public enum WithdrawalStatus
{
    Requested = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>
/// A discount code. How often it was used is counted from the orders that carry it: waiting and paid orders count, so a
/// code cannot be overdrawn by orders that are still waiting, and a refused, dropped or reversed order gives its use back.
/// </summary>
public sealed class Coupon
{
    public Guid Id { get; set; }
    /// <summary>Upper case, without spaces.</summary>
    public string Code { get; set; } = string.Empty;
    public CouponKind Kind { get; set; }
    /// <summary>Percent (1–100) or an amount in the plan's currency.</summary>
    public decimal Value { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    /// <summary>How many orders may use it in all; null = no limit.</summary>
    public int? MaxUses { get; set; }
    /// <summary>JSON array of plan ids it is good for; empty = every plan.</summary>
    public string PlanIdsJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>A buyer's request to withdraw (cayma) from a paid order within the legal time.</summary>
public sealed class Withdrawal
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public WithdrawalStatus Status { get; set; }
    public string? Reason { get; set; }
    public Guid RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? ReviewNote { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

/// <summary>The invoice of a payment, uploaded by the admin as a PDF and kept in the database.</summary>
public sealed class Invoice
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateOnly IssuedOn { get; set; }
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
    public int Size { get; set; }
    public Guid UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
}

/// <summary>Rules of coupons, trials, withdrawal and invoices, carried over from Koçluk (billing-service-design.md, section 2).</summary>
public static class ExtrasRules
{
    public const int WithdrawalDays = 14;
    public const int MaxInvoiceBytes = 10 * 1024 * 1024;

    public static string? NormalizeCouponCode(string? code)
    {
        var value = new string((code ?? string.Empty).Where(character => !char.IsWhiteSpace(character)).ToArray()).ToUpperInvariant();
        return value.Length is >= 3 and <= 30 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_') ? value : null;
    }

    /// <summary>Null when the coupon is sound; otherwise what is wrong.</summary>
    public static string? CouponProblem(CouponKind kind, decimal value, DateTime? startsAt, DateTime? endsAt, int? maxUses)
    {
        if (kind == CouponKind.Percent && value is < 1 or > 100) return "Yüzde indirimi 1 ile 100 arasında olmalıdır.";
        if (kind == CouponKind.Amount && value is <= 0 or > BillingRules.MaxPrice) return "İndirim tutarı 0'dan büyük olmalıdır.";
        if (startsAt is not null && endsAt is not null && endsAt <= startsAt) return "Bitiş tarihi başlangıçtan sonra olmalıdır.";
        if (maxUses is < 1) return "Kullanım sınırı en az 1 olmalıdır.";
        return null;
    }

    /// <summary>The discount a coupon takes off a price; never more than the price.</summary>
    public static decimal Discount(CouponKind kind, decimal value, decimal price)
    {
        var discount = kind == CouponKind.Percent ? Math.Round(price * value / 100m, 2, MidpointRounding.AwayFromZero) : value;
        return Math.Clamp(discount, 0, price);
    }

    /// <summary>Withdrawal can be asked for until this moment: 14 days from the approval.</summary>
    public static DateTime WithdrawalDeadline(DateTime approvedAt) => approvedAt.AddDays(WithdrawalDays);

    public static bool IsPdf(ReadOnlySpan<byte> content) => content.StartsWith("%PDF-"u8);

    public static string? NormalizeInvoiceNumber(string? number)
    {
        var value = number?.Trim();
        return value is { Length: >= 3 and <= 40 } ? value : null;
    }
}

/// <summary>A reminder that was sent, so that it is not sent again for the same end of the same subscription.</summary>
public sealed class NoticeReceipt
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public DateTime PeriodEnd { get; set; }
    public DateTime SentAt { get; set; }
}

/// <summary>
/// An account that was erased. Its financial records are kept for ten years (tax and commercial law) and what names
/// the person in them is removed after that.
/// </summary>
public sealed class ErasureReceipt
{
    public Guid UserId { get; set; }
    public DateTime ErasedAt { get; set; }
    public DateTime RedactAfter { get; set; }
    public DateTime? RedactedAt { get; set; }
}

public static class RetentionRules
{
    /// <summary>User decision 14.11.2026: financial records of an erased account are kept ten years.</summary>
    public const int FinancialRetentionYears = 10;
    /// <summary>
    /// User decision 15.11.2026: a bank line that was tied to no order (an unrelated transfer, say) keeps its sender's
    /// name and description for one year after the movement; the amount and date stay.
    /// </summary>
    public const int UnmatchedBankLineYears = 1;
    public const string RedactedName = "Silinmiş hesap";
    public const string RedactedText = "[Saklama süresi dolduğu için arındırıldı]";
    public const int ReminderDays = 7;
}
