using System.Globalization;
using System.Text;

namespace Coaching.Application.Subscriptions;

public static class CoachingManagementRules
{
    private static readonly HashSet<string> BillingPeriods = new(StringComparer.Ordinal)
    {
        "OneTime",
        "Monthly",
        "Quarterly",
        "Annual"
    };

    public static string NormalizeSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Replace('ı', 'i').Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(normalized.Length);
        var separatorPending = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (separatorPending && result.Length > 0)
                {
                    result.Append('-');
                }

                result.Append(character);
                separatorPending = false;
            }
            else
            {
                separatorPending = result.Length > 0;
            }
        }

        return result.ToString();
    }

    public static bool IsValidPlan(
        string audience,
        decimal price,
        bool isContactOnly,
        string billingPeriod,
        int durationDays,
        int? includedStudentSeats) =>
        (audience is "Individual" or "Institution" or "Teacher")
        && price >= 0
        && (isContactOnly ? price == 0 : price > 0)
        && BillingPeriods.Contains(billingPeriod)
        && durationDays is >= 1 and <= 3650
        && (audience is not ("Institution" or "Teacher")
            || includedStudentSeats is > 0 and <= 100_000
            || (isContactOnly && includedStudentSeats is null))
        && (audience != "Individual" || includedStudentSeats is null);

    public static string NormalizeIban(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();

    public static bool IsValidIban(string? value)
    {
        var iban = NormalizeIban(value);
        if (iban.Length is < 15 or > 34
            || iban[0] is < 'A' or > 'Z'
            || iban[1] is < 'A' or > 'Z'
            || iban[2] is < '0' or > '9'
            || iban[3] is < '0' or > '9'
            || iban.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            return false;
        }

        var rotated = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var character in rotated)
        {
            if (character is >= '0' and <= '9')
            {
                remainder = (remainder * 10 + character - '0') % 97;
            }
            else
            {
                var valueNumber = character - 'A' + 10;
                remainder = (remainder * 100 + valueNumber) % 97;
            }
        }

        return remainder == 1;
    }

    public static bool IsSafeNavigationUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(char.IsControl)
            || value.Contains('\\'))
        {
            return false;
        }

        var url = value.Trim();
        if (url.StartsWith("/", StringComparison.Ordinal))
        {
            return !url.StartsWith("//", StringComparison.Ordinal);
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https";
    }
}
