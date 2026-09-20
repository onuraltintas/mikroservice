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
            ValidateNoCaseInsensitiveDuplicateProperties(root);
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
            if (configured == "text_stream")
            {
                ValidateTextStreamConfiguration(root);
                if (nested.HasValue)
                    ValidateTextStreamConfiguration(nested.Value);
                ValidateEffectiveTextStreamConfiguration(root, nested);
            }
            if (configured == "text_fade")
            {
                ValidateTextFadeConfiguration(root);
                if (nested.HasValue)
                    ValidateTextFadeConfiguration(nested.Value);
                ValidateEffectiveTextFadeConfiguration(root, nested);
            }
            if (configured == "word_highlight")
            {
                ValidateWordHighlightConfiguration(root);
                if (nested.HasValue)
                    ValidateWordHighlightConfiguration(nested.Value);
                ValidateEffectiveWordHighlightConfiguration(root, nested);
            }
            if (configured is "scan_find" or "scanning" or "skimming")
            {
                ValidateScanConfiguration(root);
                if (nested.HasValue)
                    ValidateScanConfiguration(nested.Value);
                ValidateEffectiveScanConfiguration(root, nested);
            }
            if (configured == "motion_path")
            {
                ValidateMotionPathConfiguration(root);
                if (nested.HasValue)
                    ValidateMotionPathConfiguration(nested.Value);
                ValidateEffectiveMotionPathConfiguration(root, nested);
            }
            if (configured is "reading_comprehension" or "exam_simulation" or "free_reading")
            {
                ValidateReadingConfiguration(root);
                if (nested.HasValue)
                    ValidateReadingConfiguration(nested.Value);
                ValidateEffectiveReadingConfiguration(root, nested);
            }
            if (configured is "regression_reduction" or "subvocalization_reduction")
            {
                ValidateReadingBehaviorConfiguration(root, configured);
                if (nested.HasValue)
                    ValidateReadingBehaviorConfiguration(nested.Value, configured);
                ValidateEffectiveReadingBehaviorConfiguration(root, nested, configured);
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

    private static void ValidateTextStreamConfiguration(JsonElement config)
    {
        foreach (var name in new[] { "timing", "content", "visuals", "adaptive" })
            ValidateOptionalObject(config, name, "Metin akışı");
        ValidateOptionalIntRange(config, "displayDurationMs", 50, 5_000, "Metin akışı gösterim süresi");
        // Historical root IntervalMs is consumed by the player as a display-duration alias.
        ValidateOptionalIntRange(config, "intervalMs", 50, 5_000, "Metin akışı gösterim süresi");
        ValidateOptionalIntRange(config, "totalStimuli", 1, 500, "Metin akışı uyaran sayısı");
        ValidateOptionalIntRange(config, "totalWords", 1, 500, "Metin akışı uyaran sayısı");
        ValidateOptionalStimulusArray(config, "stimuli", 500, 1_000, "Metin akışı uyaranları");
        foreach (var alias in new[] { "words", "chunks" })
            ValidateOptionalStringArray(config, alias, 500, 1_000, "Metin akışı uyaranları");

        var timing = TryGetObject(config, "timing");
        if (timing.HasValue)
        {
            ValidateOptionalIntRange(timing.Value, "durationMs", 50, 5_000, "Metin akışı gösterim süresi");
            ValidateOptionalIntRange(timing.Value, "intervalMs", 0, 10_000, "Metin akışı bekleme süresi");
        }

        var content = TryGetObject(config, "content");
        if (content.HasValue)
        {
            ValidateOptionalIntRange(content.Value, "count", 1, 500, "Metin akışı uyaran sayısı");
            ValidateOptionalStringArray(content.Value, "items", 500, 1_000, "Metin akışı özel içeriği");
        }

        var adaptive = TryGetObject(config, "adaptive");
        if (adaptive.HasValue)
        {
            ValidateOptionalIntRange(adaptive.Value, "minDurationMs", 50, 5_000, "Adaptif minimum gösterim süresi");
            ValidateOptionalIntRange(adaptive.Value, "maxDurationMs", 50, 5_000, "Adaptif maksimum gösterim süresi");
            var minimum = ReadOptionalInt(adaptive.Value, "minDurationMs");
            var maximum = ReadOptionalInt(adaptive.Value, "maxDurationMs");
            if (minimum.HasValue && maximum.HasValue && minimum > maximum)
                throw new ArgumentException("Adaptif minimum gösterim süresi maksimum süreden büyük olamaz.");
        }

        var contentSourceCount = new[] { "stimuli", "words", "chunks" }.Count(name => TryGetProperty(config, name).HasValue);
        if (content.HasValue && TryGetProperty(content.Value, "items").HasValue)
            contentSourceCount++;
        if (contentSourceCount > 1)
            throw new ArgumentException("Metin akışı için yalnızca bir içerik kaynağı tanımlanmalıdır.");
    }

    private static void ValidateReadingBehaviorConfiguration(JsonElement config, string engineType)
    {
        ValidateOptionalObject(config, "difficultySettings", "Okuma davranışı zorluk ayarları");
        ValidateOptionalInlineText(config);
        ValidateOptionalArray(config, "questions", 100, "Okuma davranışı soruları");
        ValidateOptionalIntRange(config, "wpm", 20, 1_500, "Hedef WPM");
        ValidateOptionalIntRange(config, "targetWpm", 20, 1_500, "Hedef WPM");
        ValidateOptionalIntRange(config, "chunkSize", 1, 10, "Okuma öbek boyutu");

        if (engineType == "regression_reduction")
        {
            ValidateOptionalIntRange(config, "wordDelayMs", 40, 10_000, "Kelime gecikmesi");
            ValidateOptionalEnum(config, "maskingType", ["none", "fade", "trailing", "contingent", "ior"], "Maskeleme türü");
            return;
        }

        ValidateOptionalIntRange(config, "msPerWord", 40, 3_000, "Kelime gösterim süresi");
        ValidateOptionalIntRange(config, "metronomeBpm", 20, 300, "Metronom BPM");
        ValidateOptionalEnum(config, "displayMode", ["highlight", "rsvp", "chunk"], "Gösterim modu");

        var difficulty = TryGetObject(config, "difficultySettings");
        if (difficulty.HasValue)
        {
            ValidateOptionalIntRange(difficulty.Value, "wpm", 20, 1_500, "Hedef WPM");
            ValidateOptionalIntRange(difficulty.Value, "targetWpm", 20, 1_500, "Hedef WPM");
            ValidateOptionalIntRange(difficulty.Value, "chunkSize", 1, 10, "Okuma öbek boyutu");
            ValidateOptionalIntRange(difficulty.Value, "msPerWord", 40, 3_000, "Kelime gösterim süresi");
            ValidateOptionalIntRange(difficulty.Value, "metronomeBpm", 20, 300, "Metronom BPM");
            ValidateOptionalEnum(difficulty.Value, "displayMode", ["highlight", "rsvp", "chunk"], "Gösterim modu");
        }
    }

    private static void ValidateEffectiveReadingBehaviorConfiguration(
        JsonElement root,
        JsonElement? nested,
        string engineType)
    {
        var baseScopes = nested.HasValue ? new[] { root, nested.Value } : new[] { root };
        var scopes = baseScopes
            .SelectMany(scope => TryGetObject(scope, "difficultySettings") is { } difficulty
                ? new[] { scope, difficulty }
                : new[] { scope })
            .ToArray();
        ValidateConsistentValues(scopes.SelectMany(scope => ReadInts(scope, "wpm", "targetWpm")), "Hedef WPM");
        ValidateConsistentValues(scopes.SelectMany(scope => ReadInts(scope, "chunkSize")), "Okuma öbek boyutu");
        if (engineType == "regression_reduction")
            ValidateConsistentValues(scopes.SelectMany(scope => ReadInts(scope, "wordDelayMs")), "Kelime gecikmesi");
        else
            ValidateConsistentValues(scopes.SelectMany(scope => ReadInts(scope, "metronomeBpm")), "Metronom BPM");
    }

    private static void ValidateTextFadeConfiguration(JsonElement config)
    {
        foreach (var name in new[] { "fading", "content", "visuals" })
            ValidateOptionalObject(config, name, "Kaybolan metin");
        ValidateOptionalIntRange(config, "targetWpm", 20, 1_500, "Kaybolan metin hedef WPM");
        ValidateOptionalIntRange(config, "lagMs", 0, 10_000, "Kaybolan metin gecikmesi");
        ValidateOptionalInlineText(config);
        var fading = TryGetObject(config, "fading");
        if (!fading.HasValue)
            return;
        ValidateOptionalIntRange(fading.Value, "speedWpm", 20, 1_500, "Kaybolan metin hedef WPM");
        ValidateOptionalIntRange(fading.Value, "lagMs", 0, 10_000, "Kaybolan metin gecikmesi");
    }

    private static void ValidateWordHighlightConfiguration(JsonElement config)
    {
        foreach (var name in new[] { "pacer", "timing", "content", "visuals" })
            ValidateOptionalObject(config, name, "Kelime vurgulama");
        ValidateOptionalIntRange(config, "targetWpm", 20, 1_500, "Kelime vurgulama hedef WPM");
        ValidateOptionalIntRange(config, "chunkSize", 1, 10, "Kelime vurgulama öbek boyutu");
        var pacer = TryGetObject(config, "pacer");
        if (pacer.HasValue)
        {
            ValidateOptionalIntRange(pacer.Value, "speedWpm", 20, 1_500, "Kelime vurgulama hedef WPM");
            ValidateOptionalIntRange(pacer.Value, "chunkSize", 1, 10, "Kelime vurgulama öbek boyutu");
        }

        var timing = TryGetObject(config, "timing");
        if (timing.HasValue)
            ValidateOptionalIntRange(timing.Value, "timeLimitSec", 1, 3_600, "Kelime vurgulama süre sınırı");
        ValidateOptionalStringArray(config, "chunks", 500, 1_000, "Kelime vurgulama öbekleri");
        ValidateOptionalInlineText(config);
    }

    private static void ValidateScanConfiguration(JsonElement config)
    {
        foreach (var name in new[] { "content", "targets", "timing", "visuals" })
            ValidateOptionalObject(config, name, "Tarama egzersizi");
        ValidateOptionalIntRange(config, "timeLimitSeconds", 1, 3_600, "Tarama süre sınırı");
        ValidateOptionalIntRange(config, "timeLimit", 1, 3_600, "Tarama süre sınırı");

        if (TryGetObject(config, "timing") is { } timing)
            ValidateOptionalIntRange(timing, "timeLimitSec", 1, 3_600, "Tarama süre sınırı");
        if (TryGetObject(config, "content") is { } content)
        {
            ValidateOptionalIntRange(content, "wordCount", 1, 10_000, "Tarama metni kelime sayısı");
            ValidateOptionalBoundedString(content, "text", 100_000, "Tarama metni");
            if (TryGetProperty(content, "source") is { } source
                && (source.ValueKind != JsonValueKind.String
                    || source.GetString()?.ToLowerInvariant() is not ("text_id" or "random_text")))
            {
                throw new ArgumentException("Tarama içerik kaynağı text_id veya random_text olmalıdır.");
            }
        }
        if (TryGetObject(config, "targets") is { } targets)
        {
            ValidateOptionalStringArray(targets, "words", 100, 100, "Tarama hedefleri");
            if (TryGetProperty(targets, "caseSensitive") is { ValueKind: not JsonValueKind.True and not JsonValueKind.False })
                throw new ArgumentException("Tarama caseSensitive alanı boolean olmalıdır.");
            if (TryGetProperty(targets, "mode") is { } mode
                && (mode.ValueKind != JsonValueKind.String
                    || mode.GetString()?.ToLowerInvariant() is not ("find_all" or "find_any")))
            {
                throw new ArgumentException("Tarama hedef modu find_all veya find_any olmalıdır.");
            }
        }

        if (TryGetProperty(config, "scanningRounds") is not { } rounds)
            return;
        if (rounds.ValueKind != JsonValueKind.Array || rounds.GetArrayLength() > 50)
            throw new ArgumentException("Tarama turları en fazla 50 öğe içermelidir.");
        foreach (var round in rounds.EnumerateArray())
        {
            if (round.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Tarama turları nesne olmalıdır.");
            ValidateOptionalBoundedString(round, "textContent", 100_000, "Tarama turu metni");
            ValidateOptionalStringArray(round, "targets", 100, 100, "Tarama turu hedefleri");
            ValidateOptionalStringArray(round, "foundTargets", 100, 100, "Bulunan tarama hedefleri");
        }
    }

    private static void ValidateMotionPathConfiguration(JsonElement config)
    {
        foreach (var name in new[] { "timing", "content", "movement", "path", "target", "fixation" })
            ValidateOptionalObject(config, name, "Göz hareketi egzersizi");

        ValidateOptionalEnum(config, "mode", ["fixation", "saccade", "tracking"], "Göz hareketi modu");

        if (TryGetObject(config, "timing") is { } timing)
        {
            ValidateOptionalIntRange(timing, "durationMs", 5_000, 3_600_000, "Göz hareketi toplam süresi");
            ValidateOptionalIntRange(timing, "durationSeconds", 5, 3_600, "Göz hareketi toplam süresi");
            ValidateOptionalIntRange(timing, "totalDurationSeconds", 5, 3_600, "Göz hareketi toplam süresi");
            ValidateOptionalIntRange(timing, "holdMs", 50, 10_000, "Göz hareketi bekleme süresi");
        }
        if (TryGetObject(config, "content") is { } content)
        {
            ValidateOptionalIntRange(content, "points", 1, 500, "Sabitleme nokta sayısı");
            ValidateOptionalIntRange(content, "peripheralCount", 0, 4, "Periferik karakter sayısı");
            ValidateOptionalIntRange(content, "pointSize", 8, 200, "Göz hareketi nokta boyutu");
            ValidateOptionalEnum(content, "pattern", ["horizontal", "vertical", "random", "z-pattern", "z-flow"], "Sakkad deseni");
            ValidateOptionalEnum(content, "type", ["dot", "letter", "number", "word"], "Sakkad içerik türü");
        }
        if (TryGetObject(config, "fixation") is { } fixation)
        {
            ValidateOptionalIntRange(fixation, "points", 1, 500, "Sabitleme nokta sayısı");
            ValidateOptionalIntRange(fixation, "peripheralCount", 0, 4, "Periferik karakter sayısı");
            ValidateOptionalIntRange(fixation, "pointSize", 8, 200, "Göz hareketi nokta boyutu");
        }
        if (TryGetObject(config, "movement") is { } movement)
        {
            ValidateOptionalIntRange(movement, "speedLevel", 1, 5, "Göz hareketi hız seviyesi");
            ValidateOptionalIntRange(movement, "jumpIntervalMs", 50, 10_000, "Göz hareketi sıçrama aralığı");
            ValidateOptionalIntRange(movement, "fixationTimeMs", 50, 10_000, "Göz hareketi sabitleme süresi");
        }
        if (TryGetObject(config, "path") is { } path)
            ValidateOptionalEnum(path, "type", ["horizontal", "vertical", "circle", "infinity8", "random_point", "two_point_jump"], "Göz hareketi yol türü");
        if (TryGetObject(config, "target") is { } target)
        {
            ValidateOptionalEnum(target, "type", ["dot", "circle", "arrow"], "Göz hareketi hedef türü");
            ValidateOptionalEnum(target, "size", ["small", "medium", "large"], "Göz hareketi hedef boyutu");
        }

        ValidateOptionalMotionTargets(config, "targets");
    }

    private static void ValidateOptionalMotionTargets(JsonElement config, string propertyName)
    {
        if (TryGetProperty(config, propertyName) is not { } targets)
            return;
        if (targets.ValueKind != JsonValueKind.Array || targets.GetArrayLength() > 500)
            throw new ArgumentException("Sakkad hedefleri en fazla 500 öğe içermelidir.");
        foreach (var target in targets.EnumerateArray())
        {
            if (target.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Sakkad hedefleri nesne olmalıdır.");
            if (TryGetProperty(target, "x") is null || TryGetProperty(target, "y") is null)
                throw new ArgumentException("Sakkad hedefleri X ve Y koordinatlarını içermelidir.");
            ValidateOptionalIntRange(target, "x", 0, 100, "Sakkad hedef X koordinatı");
            ValidateOptionalIntRange(target, "y", 0, 100, "Sakkad hedef Y koordinatı");
            ValidateOptionalIntRange(target, "size", 8, 200, "Sakkad hedef boyutu");
            ValidateOptionalBoundedString(target, "value", 100, "Sakkad hedef değeri");
            if (TryGetProperty(target, "number") is { } number
                && number.ValueKind is not JsonValueKind.String and not JsonValueKind.Number)
                throw new ArgumentException("Sakkad hedef numarası metin veya sayı olmalıdır.");
            ValidateOptionalBoundedStringOrInteger(target, "number", 100, "Sakkad hedef numarası");
        }
    }

    private static void ValidateEffectiveMotionPathConfiguration(JsonElement root, JsonElement? nested)
    {
        var scopes = nested.HasValue ? new[] { root, nested.Value } : new[] { root };
        ValidateConsistentStrings(scopes.SelectMany(scope => ReadStrings(scope, "mode")), "Göz hareketi modu");
        ValidateConsistentValues(scopes.SelectMany(scope => ReadNestedInts(scope, "timing", "holdMs")
            .Concat(ReadNestedInts(scope, "movement", "fixationTimeMs"))), "Göz hareketi bekleme süresi");
        ValidateConsistentValues(scopes.SelectMany(scope => ReadNestedInts(scope, "content", "points")
            .Concat(ReadNestedInts(scope, "fixation", "points"))), "Sabitleme nokta sayısı");
        ValidateConsistentValues(scopes.SelectMany(scope => ReadNestedInts(scope, "content", "peripheralCount")
            .Concat(ReadNestedInts(scope, "fixation", "peripheralCount"))), "Periferik karakter sayısı");
        ValidateConsistentValues(scopes.SelectMany(scope => ReadNestedInts(scope, "content", "pointSize")
            .Concat(ReadNestedInts(scope, "fixation", "pointSize"))), "Göz hareketi nokta boyutu");
        ValidateConsistentValues(scopes.SelectMany(scope => ReadNestedInts(scope, "movement", "speedLevel")), "Göz hareketi hız seviyesi");
        ValidateConsistentValues(scopes.SelectMany(scope => ReadNestedInts(scope, "movement", "jumpIntervalMs")), "Göz hareketi sıçrama aralığı");
        ValidateConsistentStrings(scopes.SelectMany(scope => ReadNestedStrings(scope, "content", "pattern")), "Sakkad deseni");
        ValidateConsistentStrings(scopes.SelectMany(scope => ReadNestedStrings(scope, "content", "type")), "Sakkad içerik türü");
        ValidateConsistentStrings(scopes.SelectMany(scope => ReadNestedStrings(scope, "path", "type")), "Göz hareketi yol türü");
        ValidateConsistentStrings(scopes.SelectMany(scope => ReadNestedStrings(scope, "target", "type")), "Göz hareketi hedef türü");
        ValidateConsistentStrings(scopes.SelectMany(scope => ReadNestedStrings(scope, "target", "size")), "Göz hareketi hedef boyutu");

        var durations = new List<int>();
        foreach (var scope in scopes)
        {
            if (TryGetObject(scope, "timing") is not { } timing)
                continue;
            durations.AddRange(ReadInts(timing, "durationMs"));
            durations.AddRange(ReadInts(timing, "durationSeconds", "totalDurationSeconds").Select(seconds => seconds * 1000));
        }
        ValidateConsistentValues(durations, "Göz hareketi toplam süresi");

        if (nested.HasValue && TryGetProperty(root, "targets").HasValue && TryGetProperty(nested.Value, "targets").HasValue)
            throw new ArgumentException("Sakkad hedefleri yalnızca tek bir yapılandırma düzeyinde tanımlanmalıdır.");
    }

    private static void ValidateReadingConfiguration(JsonElement config)
    {
        ValidateOptionalObject(config, "timing", "Okuma egzersizi");
        ValidateOptionalObject(config, "display", "Okuma egzersizi");
        if (TryGetProperty(config, "content") is { } content
            && content.ValueKind is not JsonValueKind.Object and not JsonValueKind.String)
            throw new ArgumentException("Okuma egzersizi content alanı metin veya nesne olmalıdır.");
        if (TryGetProperty(config, "content") is { ValueKind: JsonValueKind.String } stringContent
            && (stringContent.GetString()?.Length ?? 0) > 100_000)
            throw new ArgumentException("Egzersiz metni en fazla 100000 karakter olmalıdır.");

        ValidateOptionalInlineText(config);
        ValidateOptionalBoundedString(config, "text", 100_000, "Egzersiz metni");
        ValidateOptionalIntRange(config, "wordCount", 1, 100_000, "Okuma kelime sayısı");
        if (TryGetObject(config, "content") is { } contentObject)
            ValidateOptionalIntRange(contentObject, "wordCount", 1, 100_000, "Okuma kelime sayısı");

        if (TryGetObject(config, "timing") is { } timing)
        {
            ValidateOptionalIntRange(timing, "minReadingTimeMs", 0, 3_600_000, "Minimum okuma süresi");
            ValidateOptionalIntRange(timing, "maxReadingTimeMs", 0, 3_600_000, "Maksimum okuma süresi");
            var minimum = ReadOptionalInt(timing, "minReadingTimeMs");
            var maximum = ReadOptionalInt(timing, "maxReadingTimeMs");
            if (minimum.HasValue && maximum is > 0 && minimum.Value > maximum.Value)
                throw new ArgumentException("Minimum okuma süresi maksimum süreden büyük olamaz.");
        }
        if (TryGetObject(config, "display") is { } display)
            ValidateOptionalEnum(display, "fontSize", ["small", "medium", "large"], "Okuma yazı boyutu");
    }

    private static void ValidateEffectiveReadingConfiguration(JsonElement root, JsonElement? nested)
    {
        var scopes = nested.HasValue ? new[] { root, nested.Value } : new[] { root };
        ValidateConsistentValues(scopes.SelectMany(scope => ReadInts(scope, "wordCount")
            .Concat(ReadNestedInts(scope, "content", "wordCount"))), "Okuma kelime sayısı");
        var minimums = scopes.SelectMany(scope => ReadNestedInts(scope, "timing", "minReadingTimeMs")).ToList();
        var maximums = scopes.SelectMany(scope => ReadNestedInts(scope, "timing", "maxReadingTimeMs")).ToList();
        ValidateConsistentValues(minimums, "Minimum okuma süresi");
        ValidateConsistentValues(maximums, "Maksimum okuma süresi");
        if (minimums.FirstOrDefault() > 0 && maximums.FirstOrDefault() > 0
            && minimums[0] > maximums[0])
            throw new ArgumentException("Minimum okuma süresi maksimum süreden büyük olamaz.");
        ValidateConsistentStrings(scopes.SelectMany(scope => ReadNestedStrings(scope, "display", "fontSize")), "Okuma yazı boyutu");
        ValidateConsistentStrings(scopes.SelectMany(ReadReadingTexts), "Okuma metni");
    }

    private static IEnumerable<string> ReadReadingTexts(JsonElement config)
    {
        foreach (var name in new[] { "readingTextContent", "text" })
        {
            if (GetString(config, name) is { } value)
                yield return value;
        }
        if (TryGetProperty(config, "content") is { ValueKind: JsonValueKind.String } stringContent)
        {
            if (stringContent.GetString() is { } value)
                yield return value;
        }
        else if (TryGetObject(config, "content") is { } content && GetString(content, "text") is { } value)
        {
            yield return value;
        }
    }

    private static void ValidateEffectiveScanConfiguration(JsonElement root, JsonElement? nested)
    {
        var limits = ReadInts(root, "timeLimitSeconds", "timeLimit");
        limits.AddRange(ReadNestedInts(root, "timing", "timeLimitSec"));
        if (nested.HasValue)
        {
            limits.AddRange(ReadInts(nested.Value, "timeLimitSeconds", "timeLimit"));
            limits.AddRange(ReadNestedInts(nested.Value, "timing", "timeLimitSec"));
        }
        ValidateConsistentValues(limits, "Tarama süre sınırı");
    }

    private static void ValidateEffectiveTextStreamConfiguration(JsonElement root, JsonElement? nested)
    {
        var contentSourceCount = CountTextStreamContentSources(root)
            + (nested.HasValue ? CountTextStreamContentSources(nested.Value) : 0);
        if (contentSourceCount > 1)
            throw new ArgumentException("Metin akışı için yalnızca bir içerik kaynağı tanımlanmalıdır.");

        var durations = ReadInts(root, "displayDurationMs", "intervalMs");
        durations.AddRange(ReadNestedInts(root, "timing", "durationMs"));
        if (nested.HasValue)
        {
            durations.AddRange(ReadInts(nested.Value, "displayDurationMs", "intervalMs"));
            durations.AddRange(ReadNestedInts(nested.Value, "timing", "durationMs"));
        }
        ValidateConsistentValues(durations, "Metin akışı gösterim süresi");

        var counts = ReadInts(root, "totalStimuli", "totalWords");
        counts.AddRange(ReadNestedInts(root, "content", "count"));
        if (nested.HasValue)
        {
            counts.AddRange(ReadInts(nested.Value, "totalStimuli", "totalWords"));
            counts.AddRange(ReadNestedInts(nested.Value, "content", "count"));
        }
        ValidateConsistentValues(counts, "Metin akışı uyaran sayısı");
    }

    private static int CountTextStreamContentSources(JsonElement config)
    {
        var count = new[] { "stimuli", "words", "chunks" }.Count(name => TryGetProperty(config, name).HasValue);
        if (TryGetObject(config, "content") is { } content && TryGetProperty(content, "items").HasValue)
            count++;
        return count;
    }

    private static void ValidateEffectiveTextFadeConfiguration(JsonElement root, JsonElement? nested)
    {
        var speeds = ReadInts(root, "targetWpm");
        speeds.AddRange(ReadNestedInts(root, "fading", "speedWpm"));
        var lags = ReadInts(root, "lagMs");
        lags.AddRange(ReadNestedInts(root, "fading", "lagMs"));
        if (nested.HasValue)
        {
            speeds.AddRange(ReadInts(nested.Value, "targetWpm"));
            speeds.AddRange(ReadNestedInts(nested.Value, "fading", "speedWpm"));
            lags.AddRange(ReadInts(nested.Value, "lagMs"));
            lags.AddRange(ReadNestedInts(nested.Value, "fading", "lagMs"));
        }
        ValidateConsistentValues(speeds, "Kaybolan metin hedef WPM");
        ValidateConsistentValues(lags, "Kaybolan metin gecikmesi");
    }

    private static void ValidateEffectiveWordHighlightConfiguration(JsonElement root, JsonElement? nested)
    {
        var speeds = ReadInts(root, "targetWpm");
        speeds.AddRange(ReadNestedInts(root, "pacer", "speedWpm"));
        var chunks = ReadInts(root, "chunkSize");
        chunks.AddRange(ReadNestedInts(root, "pacer", "chunkSize"));
        if (nested.HasValue)
        {
            speeds.AddRange(ReadInts(nested.Value, "targetWpm"));
            speeds.AddRange(ReadNestedInts(nested.Value, "pacer", "speedWpm"));
            chunks.AddRange(ReadInts(nested.Value, "chunkSize"));
            chunks.AddRange(ReadNestedInts(nested.Value, "pacer", "chunkSize"));
        }
        ValidateConsistentValues(speeds, "Kelime vurgulama hedef WPM");
        ValidateConsistentValues(chunks, "Kelime vurgulama öbek boyutu");
    }

    private static List<int> ReadInts(JsonElement config, params string[] names) =>
        names.Select(name => ReadOptionalInt(config, name)).Where(value => value.HasValue).Select(value => value!.Value).ToList();

    private static List<int> ReadNestedInts(JsonElement config, string objectName, params string[] names) =>
        TryGetObject(config, objectName) is { } nested ? ReadInts(nested, names) : [];

    private static IEnumerable<string> ReadStrings(JsonElement config, params string[] names) =>
        names.Select(name => GetString(config, name)).Where(value => value is not null).Select(value => value!);

    private static IEnumerable<string> ReadNestedStrings(JsonElement config, string objectName, params string[] names) =>
        TryGetObject(config, objectName) is { } nested ? ReadStrings(nested, names) : [];

    private static void ValidateConsistentValues(IEnumerable<int> values, string displayName)
    {
        if (values.Distinct().Skip(1).Any())
            throw new ArgumentException($"{displayName} için tanımlanan değerler uyuşmalıdır.");
    }

    private static void ValidateConsistentStrings(IEnumerable<string> values, string displayName)
    {
        if (values.Distinct(StringComparer.OrdinalIgnoreCase).Skip(1).Any())
            throw new ArgumentException($"{displayName} için tanımlanan değerler uyuşmalıdır.");
    }

    private static void ValidateOptionalEnum(
        JsonElement config,
        string propertyName,
        IReadOnlyCollection<string> supportedValues,
        string displayName)
    {
        if (TryGetProperty(config, propertyName) is not { } property)
            return;
        if (property.ValueKind != JsonValueKind.String
            || property.GetString() is not { } value
            || !supportedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"{displayName} desteklenen değerlerden biri olmalıdır.");
    }

    private static void ValidateOptionalStringArray(
        JsonElement config,
        string propertyName,
        int maximumLength,
        int maximumItemLength,
        string displayName)
    {
        if (TryGetProperty(config, propertyName) is not { } property)
            return;
        if (property.ValueKind != JsonValueKind.Array || property.GetArrayLength() > maximumLength)
            throw new ArgumentException($"{displayName} en fazla {maximumLength} öğe içermelidir.");
        if (property.EnumerateArray().Any(item =>
                item.ValueKind != JsonValueKind.String || (item.GetString()?.Length ?? 0) > maximumItemLength))
        {
            throw new ArgumentException($"{displayName} yalnızca en fazla {maximumItemLength} karakterlik metinler içermelidir.");
        }
    }

    private static void ValidateOptionalStimulusArray(
        JsonElement config,
        string propertyName,
        int maximumLength,
        int maximumItemLength,
        string displayName)
    {
        if (TryGetProperty(config, propertyName) is not { } property)
            return;
        if (property.ValueKind != JsonValueKind.Array || property.GetArrayLength() > maximumLength)
            throw new ArgumentException($"{displayName} en fazla {maximumLength} öğe içermelidir.");
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && (item.GetString()?.Length ?? 0) <= maximumItemLength)
                continue;
            if (item.ValueKind == JsonValueKind.Object
                && GetString(item, "text") is { Length: > 0 } text
                && text.Length <= maximumItemLength)
            {
                continue;
            }
            throw new ArgumentException($"{displayName} geçerli ve en fazla {maximumItemLength} karakterlik metinler içermelidir.");
        }
    }

    private static void ValidateOptionalObject(JsonElement config, string propertyName, string displayName)
    {
        if (TryGetProperty(config, propertyName) is { } property && property.ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"{displayName} {propertyName} alanı bir nesne olmalıdır.");
    }

    private static void ValidateOptionalInlineText(JsonElement config)
    {
        if (TryGetProperty(config, "readingTextContent") is { } legacyText
            && (legacyText.ValueKind != JsonValueKind.String || (legacyText.GetString()?.Length ?? 0) > 100_000))
        {
            throw new ArgumentException("Egzersiz metni en fazla 100000 karakter olmalıdır.");
        }
        if (TryGetObject(config, "content") is not { } content
            || TryGetProperty(content, "text") is not { } text)
            return;
        if (text.ValueKind != JsonValueKind.String || (text.GetString()?.Length ?? 0) > 100_000)
            throw new ArgumentException("Egzersiz metni en fazla 100000 karakter olmalıdır.");
    }

    private static void ValidateOptionalBoundedString(
        JsonElement config,
        string propertyName,
        int maximumLength,
        string displayName)
    {
        if (TryGetProperty(config, propertyName) is not { } property)
            return;
        if (property.ValueKind != JsonValueKind.String || (property.GetString()?.Length ?? 0) > maximumLength)
            throw new ArgumentException($"{displayName} en fazla {maximumLength} karakter olmalıdır.");
    }

    private static void ValidateOptionalBoundedStringOrInteger(
        JsonElement config,
        string propertyName,
        int maximumLength,
        string displayName)
    {
        if (TryGetProperty(config, propertyName) is not { } property)
            return;
        if (property.ValueKind == JsonValueKind.String && (property.GetString()?.Length ?? 0) <= maximumLength)
            return;
        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out _))
            return;
        throw new ArgumentException($"{displayName} geçerli ve en fazla {maximumLength} karakter olmalıdır.");
    }

    private static void ValidateNoCaseInsensitiveDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new ArgumentException("Yapılandırmada büyük/küçük harf farkıyla yinelenen alanlar kullanılamaz.");
                ValidateNoCaseInsensitiveDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                ValidateNoCaseInsensitiveDuplicateProperties(item);
        }
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
