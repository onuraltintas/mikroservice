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

    /// <summary>A plan can be bought online when it has a price and is not a "contact us" plan.</summary>
    public static bool CanBeOrdered(decimal price, bool isContactOnly) => price > 0 && !isContactOnly;

    [GeneratedRegex("^TR\\d{24}$", RegexOptions.CultureInvariant)]
    private static partial Regex TurkishIban();

    [GeneratedRegex("^[a-z][a-z0-9-]{1,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex Code();

    [GeneratedRegex("^[a-z][a-z0-9-]{1,39}\\.[a-z][a-z0-9-]{1,59}$", RegexOptions.CultureInvariant)]
    private static partial Regex EntitlementKey();
}
