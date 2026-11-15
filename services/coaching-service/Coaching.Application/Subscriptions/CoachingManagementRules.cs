using System.Globalization;
using System.Text;

namespace Coaching.Application.Subscriptions;

public static class CoachingManagementRules
{
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
