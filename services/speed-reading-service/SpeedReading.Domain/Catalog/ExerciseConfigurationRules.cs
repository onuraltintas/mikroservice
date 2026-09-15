using System.Text.Json;

namespace SpeedReading.Domain.Catalog;

/// <summary>
/// The server-side counterpart of the student player's supported engines.
/// It accepts historical type names only to normalize them while an editor
/// updates old catalog rows; new configurations are stored canonically.
/// </summary>
public static class ExerciseConfigurationRules
{
    private static readonly IReadOnlySet<string> SupportedEngines = new HashSet<string>(StringComparer.Ordinal)
    {
        "grid_interaction", "text_stream", "motion_path", "text_fade", "word_highlight",
        "visual_expansion", "scan_find", "reading_comprehension", "exam_simulation",
        "free_reading", "regression_reduction", "subvocalization_reduction", "visualization",
        "attention_training", "focus", "vocabulary_builder", "error_analysis",
        "adaptive_fluency", "scanning", "skimming"
    };

    private static readonly IReadOnlyDictionary<string, string> LegacyAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["schultetable"] = "grid_interaction",
            ["eye_tracking"] = "motion_path",
            ["eyetracking"] = "motion_path",
            ["fixation"] = "motion_path",
            ["saccade"] = "motion_path",
            ["tachistoscope"] = "text_stream",
            ["rsvp"] = "text_stream",
            ["textfading"] = "text_fade",
            ["text_fading"] = "text_fade",
            ["speedreading"] = "word_highlight",
            ["speed_reading"] = "word_highlight",
            ["chunking"] = "word_highlight",
            ["word_group"] = "word_highlight",
            ["visualexpansion"] = "visual_expansion",
            ["scanning"] = "scanning",
            ["skimming"] = "skimming",
            ["comprehension"] = "reading_comprehension",
            ["examsimulation"] = "exam_simulation",
            ["freereading"] = "free_reading",
            ["regressionreduction"] = "regression_reduction",
            ["subvocalizationreduction"] = "subvocalization_reduction",
            ["attentiontraining"] = "attention_training",
            ["vocabulary"] = "vocabulary_builder",
            ["vocabularybuilder"] = "vocabulary_builder",
            ["erroranalysis"] = "error_analysis",
            ["adaptivefluency"] = "adaptive_fluency"
        };

    public static IReadOnlyList<string> GetSupportedEngineTypes() =>
        SupportedEngines.Order(StringComparer.Ordinal).ToList();

    public static string NormalizeEngineType(string engineType)
    {
        if (string.IsNullOrWhiteSpace(engineType))
            throw new ArgumentException("Egzersiz motoru seçilmelidir.", nameof(engineType));

        var candidate = engineType.Trim().Replace('-', '_').Replace(' ', '_').ToLowerInvariant();
        if (LegacyAliases.TryGetValue(candidate, out var aliased))
            candidate = aliased;
        if (!SupportedEngines.Contains(candidate))
            throw new ArgumentException("Öğrenci oynatıcısının desteklemediği bir egzersiz motoru seçildi.", nameof(engineType));
        return candidate;
    }

    public static void ValidateActiveConfiguration(string configurationJson, string expectedEngineType)
    {
        var expected = NormalizeEngineType(expectedEngineType);
        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Egzersiz yapılandırması bir JSON nesnesi olmalıdır.", nameof(configurationJson));

            var root = document.RootElement;
            var nested = TryGetObject(root, "engineConfig");
            var rootEngine = GetString(root, "engineType");
            var nestedEngine = nested.HasValue ? GetString(nested.Value, "engineType") : null;
            if (string.IsNullOrWhiteSpace(rootEngine) && string.IsNullOrWhiteSpace(nestedEngine))
                throw new ArgumentException("Aktif egzersiz için desteklenen bir motor seçilmelidir.", nameof(configurationJson));

            var configured = NormalizeEngineType(rootEngine ?? nestedEngine!);
            if (!string.IsNullOrWhiteSpace(nestedEngine)
                && !string.Equals(configured, NormalizeEngineType(nestedEngine), StringComparison.Ordinal))
            {
                throw new ArgumentException("Yapılandırmadaki motor tanımları birbiriyle uyuşmuyor.", nameof(configurationJson));
            }
            if (!string.Equals(configured, expected, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Egzersiz yapılandırmasının motoru seçilen egzersiz türüyle uyuşmuyor.",
                    nameof(configurationJson));
            }

            if (configured == "vocabulary_builder")
                ValidateVocabularyConfiguration(root, nested);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Egzersiz yapılandırması geçerli JSON olmalıdır.", nameof(configurationJson), exception);
        }
    }

    private static void ValidateVocabularyConfiguration(JsonElement root, JsonElement? nested)
    {
        var config = nested ?? root;
        if (TryGetObject(config, "vocabulary") is not { } vocabulary)
            return;

        if (vocabulary.TryGetProperty("count", out var count)
            && (!count.TryGetInt32(out var countValue) || countValue is < 1 or > 50))
        {
            throw new ArgumentException("Kelime egzersizinde sayı 1 ile 50 arasında olmalıdır.");
        }
        if (vocabulary.TryGetProperty("difficultyLevel", out var difficulty)
            && (!difficulty.TryGetInt32(out var difficultyValue) || difficultyValue is < 1 or > 5))
        {
            throw new ArgumentException("Kelime egzersizinde zorluk 1 ile 5 arasında olmalıdır.");
        }
    }

    private static JsonElement? TryGetObject(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : null;

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;
}
