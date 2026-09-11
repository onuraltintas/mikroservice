namespace SpeedReading.Application.ExerciseSessions;

public sealed record SpeedReadingMeasurementCapability(
    string Code,
    string DisplayName,
    string MeasurementMode,
    bool IsAssessmentEligible,
    string Evidence);

public static class SpeedReadingMeasurementCapabilities
{
    public static IReadOnlyList<SpeedReadingMeasurementCapability> Definitions { get; } =
    [
        new("reading", "Sunucu zamanlı okuma", "ServerTimedReading", true, "Sunucu başlangıç/bitiş zamanı, WPM ve varsa kavrama"),
        new("visual_expansion", "Görsel genişleme", "ValidatedInteraction", true, "Sunucu uyaranı, tur sırası, cevap ve tepki penceresi"),
        new("focus", "Odak ve dikkat", "ValidatedInteraction", true, "Sunucu uyaran akışı, N-back hedefi ve cevap penceresi"),
        new("schulte", "Schulte ve grid", "ValidatedInteraction", true, "Sunucu yerleşimi ve beklenen tıklama sırası"),
        new("visualization", "Görselleştirme", "ValidatedQuestion", true, "Sunucu soru bankası ve cevap anahtarı"),
        new("motion_path", "Hareket yolu ve göz takibi", "NotMeasured", false, "Doğrulanmış göz izleme donanımı veya sunucu sinyali yok")
    ];

    public static bool IsAssessmentEligible(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return false;
        return Contains(typeName, "speedreading", "rsvp", "tachistoscope", "comprehension", "reading", "free",
            "chunking", "textfading", "skimming", "scanning", "visualexpansion", "visual expansion",
            "görsel genişleme", "visualization", "visualisation", "schulte", "grid", "focus", "attention", "fixation");
    }

    private static bool Contains(string value, params string[] parts) =>
        parts.Any(part => value.Contains(part, StringComparison.OrdinalIgnoreCase));
}
