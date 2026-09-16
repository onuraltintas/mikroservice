using System.Text.Json;
using System.Linq;

namespace SpeedReading.Application.Content;

public static class EvidenceMetricRules
{
    public const string Group = "EvidenceMetrics";

    public static bool IsPubliclyVisible(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("isVisible", out var isVisible)
                || isVisible.ValueKind is not JsonValueKind.True)
            {
                return false;
            }

            // Numeric outcome claims require an explicit verification marker. This keeps
            // legacy/demo figures out of the public page until their source is reviewed.
            if (!root.TryGetProperty("value", out var metricValue))
            {
                return true;
            }

            // A numeric JSON value is still an outcome claim. Require editors to
            // store it as reviewed text so it cannot bypass the verification flag.
            if (metricValue.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var valueText = metricValue.GetString()?.Trim() ?? string.Empty;
            var containsNumericClaim = valueText.Any(char.IsDigit);
            return !containsNumericClaim
                || root.TryGetProperty("verified", out var verified)
                    && verified.ValueKind is JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
