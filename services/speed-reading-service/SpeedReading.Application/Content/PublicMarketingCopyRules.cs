using System.Text.Json;
using System.Text.RegularExpressions;
using EduPlatform.Shared.Kernel.Exceptions;

namespace SpeedReading.Application.Content;

/// <summary>
/// Keeps unsupported outcome and speed claims out of publicly published CMS content.
/// </summary>
public static class PublicMarketingCopyRules
{
    private static readonly Regex[] UnsafePatterns =
    [
        new(@"\b\d+\s*kat(?:la\w*|lan\w*|ına|ını|ı|a)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bikiye\s+kat(?:la\w*|lan\w*)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b\d{2,4}\s*\+?\s*wpm\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"(?:\b\d{1,3}\s*%|%\s*\d{1,3}\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"(?:\byüzde\s+\d{1,3}\b|\b\d{1,3}\s+yüzde\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bkesin\w*\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bgaranti\w*\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
    ];

    public static bool IsSafe(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && !ContainsUnsafeCopy(document.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static void EnsureSafe(string? json)
    {
        if (!IsSafe(json))
        {
            throw new BusinessRuleException(
                "Cms.PublicMarketingCopy",
                "Ana sayfa metni doğrulanmamış hız, yüzde veya kesin sonuç vaadi içeremez.");
        }
    }

    private static bool ContainsUnsafeCopy(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => UnsafePatterns.Any(pattern => pattern.IsMatch(element.GetString() ?? string.Empty)),
            JsonValueKind.Object => element.EnumerateObject().Any(property => ContainsUnsafeCopy(property.Value)),
            JsonValueKind.Array => element.EnumerateArray().Any(ContainsUnsafeCopy),
            _ => false
        };
    }
}
