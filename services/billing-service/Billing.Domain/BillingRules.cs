using System.Text.RegularExpressions;

namespace Billing.Domain;

/// <summary>The catalog rules carried over from Koçluk and Hızlı Okuma (billing-service-design.md, section 2).</summary>
public static partial class BillingRules
{
    public const string DefaultCurrency = "TRY";
    public const int MaxDurationDays = 3650;
    public const int MaxTrialDays = 90;
    public const decimal MaxPrice = 1_000_000m;
    public const int MaxSeats = 100_000;

    public static string? NormalizeIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return null;
        var normalized = string.Concat(iban.Where(char.IsLetterOrDigit)).ToUpperInvariant();
        return TurkishIban().IsMatch(normalized) ? normalized : null;
    }

    /// <summary>TR33 0006 1005 1978 6457 8413 26.</summary>
    public static string FormatIban(string iban) =>
        string.Join(' ', Enumerable.Range(0, (iban.Length + 3) / 4).Select(index => iban.Substring(index * 4, Math.Min(4, iban.Length - index * 4))));

    public static string? NormalizeCode(string? code)
    {
        var value = code?.Trim().ToLowerInvariant();
        return value is not null && Code().IsMatch(value) ? value : null;
    }

    /// <summary>An entitlement key belongs to its product: "coaching.student" under "coaching".</summary>
    public static string? NormalizeEntitlementKey(string? key, string productCode)
    {
        var value = key?.Trim().ToLowerInvariant();
        return value is not null && EntitlementKey().IsMatch(value) && value.StartsWith(productCode + ".", StringComparison.Ordinal) ? value : null;
    }

    /// <summary>Null when the plan is sound; otherwise what is wrong, in the admin's language.</summary>
    public static string? PlanProblem(BuyerType buyerType, decimal price, int durationDays, int trialDays, int? seatCount, decimal? extraSeatPrice, bool isContactOnly)
    {
        if (price < 0 || price > MaxPrice) return "Fiyat 0 ile 1.000.000 arasında olmalıdır.";
        if (durationDays is < 1 or > MaxDurationDays) return "Süre 1 ile 3650 gün arasında olmalıdır.";
        if (trialDays is < 0 or > MaxTrialDays) return "Deneme süresi 0–90 gün olabilir.";
        if (trialDays > 0 && buyerType == BuyerType.Institution) return "Kurum planlarında deneme süresi olmaz.";
        if (seatCount is < 1 or > MaxSeats) return "Koltuk sayısı 1 ile 100.000 arasında olmalıdır.";
        if (seatCount is not null && buyerType == BuyerType.Individual) return "Bireysel planda koltuk olmaz.";
        if (extraSeatPrice is not null && (buyerType != BuyerType.Institution || seatCount is null || extraSeatPrice <= 0 || extraSeatPrice > MaxPrice))
            return "Ek koltuk fiyatı yalnız koltuklu kurum planlarında, 0'dan büyük ve en çok 1.000.000 olabilir.";
        if (isContactOnly && price != 0) return "\"İletişime geçin\" planının fiyatı 0 olmalıdır.";
        return null;
    }

    public const int MaxExtraSeatsPerOrder = 1_000;

    /// <summary>How many seats a subscription has now: the plan's, plus the extra ones while they last.</summary>
    public static int SeatCapacity(int? seatCount, int extraSeats, DateTime? extraSeatsUntil, DateTime now) =>
        (seatCount ?? 0) + (extraSeats > 0 && extraSeatsUntil is { } until && until > now ? extraSeats : 0);

    /// <summary>
    /// The price of extra seats for the days left of the running period (carried over from Koçluk): the plan's extra
    /// seat price is for a whole period; a started day counts.
    /// </summary>
    public static decimal ExtraSeatsPrice(decimal extraSeatPrice, int seats, int durationDays, DateTime now, DateTime endsAt)
    {
        var daysLeft = Math.Clamp((int)Math.Ceiling((endsAt - now).TotalDays), 0, durationDays);
        return Math.Round(extraSeatPrice * seats * daysLeft / durationDays, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>A pending order waits this long for its payment, then drops (carried over from Koçluk).</summary>
    public const int PendingOrderDays = 14;
    public const int MaxReceiptBytes = 5 * 1024 * 1024;
    public const string PaymentCodePrefix = "EDU-";
    /// <summary>No 0/O, 1/I/L: the code is typed by hand into a bank's description field.</summary>
    public const string PaymentCodeAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    public const int PaymentCodeLength = 6;

    /// <summary>EDU-7K4Q2M: short enough to type, 31^6 (about 887 million) values.</summary>
    public static string NewPaymentCode(Func<int, int> nextIndex) =>
        PaymentCodePrefix + string.Concat(Enumerable.Range(0, PaymentCodeLength).Select(_ => PaymentCodeAlphabet[nextIndex(PaymentCodeAlphabet.Length)]));

    /// <summary>The content type of a receipt by its first bytes (PDF, JPEG, PNG); null for anything else.</summary>
    public static string? ReceiptContentType(ReadOnlySpan<byte> content)
    {
        if (content.StartsWith("%PDF-"u8)) return "application/pdf";
        if (content.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return "image/jpeg";
        if (content.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        return null;
    }

    /// <summary>
    /// When a new paid period starts and ends: right away, or on the day the running subscription ends, so a renewal
    /// never loses days.
    /// </summary>
    public static (DateTime StartsAt, DateTime EndsAt) NextPeriod(DateTime now, DateTime? runningUntil, int durationDays)
    {
        var start = runningUntil is { } end && end > now ? end : now;
        return (start, start.AddDays(durationDays));
    }

    /// <summary>A plan can be bought online when it has a price and is not a "contact us" plan.</summary>
    public static bool CanBeOrdered(decimal price, bool isContactOnly) => price > 0 && !isContactOnly;

    [GeneratedRegex("^TR\\d{24}$", RegexOptions.CultureInvariant)]
    private static partial Regex TurkishIban();

    [GeneratedRegex("^[a-z][a-z0-9-]{1,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex Code();

    [GeneratedRegex("^[a-z][a-z0-9-]{1,39}\\.[a-z][a-z0-9-]{1,59}$", RegexOptions.CultureInvariant)]
    private static partial Regex EntitlementKey();
}
