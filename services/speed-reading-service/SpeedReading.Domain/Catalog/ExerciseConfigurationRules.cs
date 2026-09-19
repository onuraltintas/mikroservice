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
            if (configured == "grid_interaction")
            {
                ValidateGridConfiguration(root);
                if (nested.HasValue)
                {
                    ValidateGridConfiguration(nested.Value);
                    ValidateMatchingGridConfiguration(root, nested.Value);
                }
            }
            if (configured == "visual_expansion")
            {
                ValidateVisualExpansionConfiguration(root);
                if (nested.HasValue)
                    ValidateVisualExpansionConfiguration(nested.Value);
                ValidateEffectiveVisualExpansionConfiguration(root, nested);
            }
            if (configured is "focus" or "attention_training")
            {
                ValidateFocusConfiguration(root);
                if (nested.HasValue)
                    ValidateFocusConfiguration(nested.Value);
                ValidateEffectiveFocusConfiguration(root, nested);
            }
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

    private static void ValidateGridConfiguration(JsonElement config)
    {
        if (TryGetProperty(config, "gridSize") is { } gridSize
            && (!gridSize.TryGetInt32(out var size) || size is < 3 or > 7))
        {
            throw new ArgumentException("Grid gridSize değeri 3 ile 7 arasında olmalıdır.");
        }

        if (TryGetProperty(config, "sequenceType") is { } sequenceType
            && (sequenceType.ValueKind != JsonValueKind.String
                || !string.Equals(sequenceType.GetString(), "numeric", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Grid sıra türü numeric olmalıdır.");
        }
    }

    private static void ValidateMatchingGridConfiguration(JsonElement root, JsonElement nested)
    {
        var rootSize = TryGetProperty(root, "gridSize");
        var nestedSize = TryGetProperty(nested, "gridSize");
        if (rootSize.HasValue && nestedSize.HasValue
            && (!rootSize.Value.TryGetInt32(out var rootSizeValue)
                || !nestedSize.Value.TryGetInt32(out var nestedSizeValue)
                || rootSizeValue != nestedSizeValue))
        {
            throw new ArgumentException("Kök ve engineConfig gridSize değerleri uyuşmalıdır.");
        }

        var rootSequence = GetString(root, "sequenceType");
        var nestedSequence = GetString(nested, "sequenceType");
        if (rootSequence is not null && nestedSequence is not null
            && !rootSequence.Equals(nestedSequence, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Kök ve engineConfig grid sıra türleri uyuşmalıdır.");
        }
    }

    private static void ValidateVisualExpansionConfiguration(JsonElement config)
    {
        ValidateOptionalIntRange(config, "rounds", 1, 100, "Görsel genişleme tur sayısı");
        ValidateOptionalIntRange(config, "totalSteps", 1, 100, "Görsel genişleme tur sayısı");
        ValidateOptionalIntRange(config, "itemCount", 1, 100, "Görsel genişleme tur sayısı");
        ValidateOptionalIntRange(config, "displayDurationMs", 100, 5_000, "Görsel genişleme gösterim süresi");

        var timing = TryGetObject(config, "timing");
        if (timing.HasValue)
            ValidateOptionalIntRange(timing.Value, "durationMs", 100, 5_000, "Görsel genişleme gösterim süresi");

        var expansion = TryGetObject(config, "expansion");
        ValidateOptionalIntRange(config, "startDegrees", 2, 60, "Görsel genişleme başlangıç açısı");
        ValidateOptionalIntRange(config, "targetDegrees", 2, 60, "Görsel genişleme hedef açısı");
        if (expansion.HasValue)
        {
            ValidateOptionalIntRange(expansion.Value, "startDegrees", 2, 60, "Görsel genişleme başlangıç açısı");
            ValidateOptionalIntRange(expansion.Value, "targetDegrees", 2, 60, "Görsel genişleme hedef açısı");
        }
        var startDegrees = ReadOptionalInt(expansion ?? config, "startDegrees")
            ?? ReadOptionalInt(config, "startDegrees");
        var targetDegrees = ReadOptionalInt(expansion ?? config, "targetDegrees")
            ?? ReadOptionalInt(config, "targetDegrees");
        if (startDegrees.HasValue && targetDegrees.HasValue && targetDegrees < startDegrees)
            throw new ArgumentException("Görsel genişleme hedef açısı başlangıç açısından küçük olamaz.");
    }

    private static void ValidateFocusConfiguration(JsonElement config)
    {
        var mode = GetString(config, "mode");
        if (mode is not null && mode.ToLowerInvariant() is not ("position" or "word" or "dual"))
            throw new ArgumentException("Focus modu position, word veya dual olmalıdır.");

        ValidateOptionalIntRange(config, "nLevel", 1, 5, "Focus N-back seviyesi");
        ValidateOptionalIntRange(config, "speedMs", 100, 10_000, "Focus uyaran süresi");
        ValidateOptionalIntRange(config, "gridSize", 3, 7, "Focus grid boyutu");
        ValidateOptionalIntRange(config, "totalSteps", 1, 500, "Focus adım sayısı");
        ValidateOptionalIntRange(config, "itemCount", 1, 500, "Focus adım sayısı");
        ValidateOptionalIntRange(config, "rounds", 1, 500, "Focus adım sayısı");
        ValidateOptionalArray(config, "positionSequence", 500, "Focus konum dizisi");
        ValidateOptionalArray(config, "wordSequence", 500, "Focus kelime dizisi");
        ValidateOptionalArray(config, "positionTargetIndices", 500, "Focus konum hedefleri");
        ValidateOptionalArray(config, "wordTargetIndices", 500, "Focus kelime hedefleri");
    }

    private static void ValidateEffectiveVisualExpansionConfiguration(JsonElement root, JsonElement? nested)
    {
        ValidateConsistentStepAliases(root, nested, 100, "Görsel genişleme tur sayısı");
        var effective = nested ?? root;
        var effectiveExpansion = TryGetObject(effective, "expansion");
        var start = ReadOptionalInt(effectiveExpansion ?? effective, "startDegrees")
            ?? ReadOptionalInt(effective, "startDegrees")
            ?? ReadOptionalInt(root, "startDegrees");
        var target = ReadOptionalInt(effectiveExpansion ?? effective, "targetDegrees")
            ?? ReadOptionalInt(effective, "targetDegrees")
            ?? ReadOptionalInt(root, "targetDegrees");
        if (start.HasValue && target.HasValue && target < start)
            throw new ArgumentException("Görsel genişleme hedef açısı başlangıç açısından küçük olamaz.");
    }

    private static void ValidateEffectiveFocusConfiguration(JsonElement root, JsonElement? nested) =>
        ValidateConsistentStepAliases(root, nested, 500, "Focus adım sayısı");

    private static void ValidateConsistentStepAliases(
        JsonElement root,
        JsonElement? nested,
        int maximum,
        string displayName)
    {
        var values = new List<int>();
        foreach (var config in nested.HasValue ? new[] { root, nested.Value } : new[] { root })
        {
            foreach (var name in new[] { "totalSteps", "itemCount", "rounds" })
            {
                if (TryGetProperty(config, name) is not { } property)
                    continue;
                if (!property.TryGetInt32(out var value) || value is < 1 || value > maximum)
                    throw new ArgumentException($"{displayName} 1 ile {maximum} arasında olmalıdır.");
                values.Add(value);
            }
        }

        if (values.Distinct().Skip(1).Any())
            throw new ArgumentException($"{displayName} için tanımlanan değerler uyuşmalıdır.");
    }

    private static void ValidateOptionalArray(
        JsonElement config,
        string propertyName,
        int maximumLength,
        string displayName)
    {
        if (TryGetProperty(config, propertyName) is not { } property)
            return;
        if (property.ValueKind != JsonValueKind.Array || property.GetArrayLength() > maximumLength)
            throw new ArgumentException($"{displayName} en fazla {maximumLength} öğe içermelidir.");
    }

    private static void ValidateOptionalIntRange(
        JsonElement config,
        string propertyName,
        int minimum,
        int maximum,
        string displayName)
    {
        if (TryGetProperty(config, propertyName) is not { } property)
            return;
        if (!property.TryGetInt32(out var value) || value < minimum || value > maximum)
            throw new ArgumentException($"{displayName} {minimum} ile {maximum} arasında olmalıdır.");
    }

    private static int? ReadOptionalInt(JsonElement config, string propertyName) =>
        TryGetProperty(config, propertyName) is { } property && property.TryGetInt32(out var value)
            ? value
            : null;

    private static JsonElement? TryGetObject(JsonElement element, string name) =>
        TryGetProperty(element, name) is { ValueKind: JsonValueKind.Object } value
            ? value
            : null;

    private static string? GetString(JsonElement element, string name) =>
        TryGetProperty(element, name) is { ValueKind: JsonValueKind.String } value
            ? value.GetString()?.Trim()
            : null;

    private static JsonElement? TryGetProperty(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        return null;
    }
}
