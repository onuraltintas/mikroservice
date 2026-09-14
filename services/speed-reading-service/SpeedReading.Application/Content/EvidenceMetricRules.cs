using System.Text.Json;

namespace SpeedReading.Application.Content;

public static class EvidenceMetricRules
{
    public const string Group = "EvidenceMetrics";

    public static bool IsPubliclyVisible(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("isVisible", out var isVisible)
                && isVisible.ValueKind is JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
