using System.Text;
using System.Text.RegularExpressions;

namespace SpeedReading.Application.Subscription;

public static partial class BankTransferPaymentRules
{
    public const string PendingStatus = "Pending";
    public const string ApprovedStatus = "Approved";
    public const string RejectedStatus = "Rejected";

    private static readonly HashSet<string> SupportedBillingPeriods =
    ["OneTime", "Monthly", "Quarterly", "Annual"];

    public static bool HasCompletePublicSettings(
        bool enabled,
        string? accountHolder,
        string? bankName,
        string? iban) =>
        enabled
        && !string.IsNullOrWhiteSpace(accountHolder)
        && !string.IsNullOrWhiteSpace(bankName)
        && NormalizeIban(iban) is not null;

    public static bool IsValidPlanDefinition(decimal price, string? billingPeriod, int? durationDays) =>
        price >= 0
        && SupportedBillingPeriods.Contains(billingPeriod?.Trim() ?? string.Empty)
        && durationDays is > 0 and <= 3650;

    public static bool IsValidPlanDefinition(decimal price, string? billingPeriod, int? durationDays, bool isContactOnly) =>
        IsValidPlanDefinition(price, billingPeriod, durationDays)
        && (!isContactOnly || price == 0);

    public static bool CanRequestBankTransfer(decimal price, bool isContactOnly) =>
        price > 0 && !isContactOnly;

    public static bool IsPubliclyPurchasable(
        bool planActive,
        bool planPublic,
        bool productActive,
        bool productPublic) =>
        planActive && planPublic && productActive && productPublic;

    public static bool IsInstitutionAccessPlan(bool planActive, bool planPublic, bool isContactOnly, int? durationDays) =>
        planActive && durationDays == 365 && (!planPublic || isContactOnly);

    public static string? NormalizeIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return null;

        var normalized = string.Concat(iban.Where(char.IsLetterOrDigit)).ToUpperInvariant();
        return TurkishIbanPattern().IsMatch(normalized) ? normalized : null;
    }

    public static string? NormalizePaymentReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;

        var builder = new StringBuilder();
        foreach (var character in reference.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_')
            {
                builder.Append(character);
            }
            else if (character is '/' or ' ')
            {
                builder.Append('-');
            }
            else
            {
                return null;
            }
        }

        var normalized = builder.ToString().Trim('-');
        return normalized.Length is >= 6 and <= 100 ? normalized : null;
    }

    public static bool CanTransition(string? currentStatus, string? nextStatus) =>
        string.Equals(currentStatus, PendingStatus, StringComparison.OrdinalIgnoreCase)
        && (string.Equals(nextStatus, ApprovedStatus, StringComparison.OrdinalIgnoreCase)
            || string.Equals(nextStatus, RejectedStatus, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex("^TR\\d{24}$", RegexOptions.CultureInvariant)]
    private static partial Regex TurkishIbanPattern();
}
