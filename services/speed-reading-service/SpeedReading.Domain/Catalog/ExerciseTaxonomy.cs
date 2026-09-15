namespace SpeedReading.Domain.Catalog;

public sealed record ExerciseTaxonomyCategory(
    string Key,
    string DisplayName,
    string Description,
    int SortOrder);

public static class ExerciseTaxonomy
{
    private static readonly IReadOnlyList<ExerciseTaxonomyCategory> Categories =
    [
        new("attention-and-eye-movement", "Dikkat ve göz hareketi", "Görsel dikkat, odak ve göz hareketi çalışmaları.", 10),
        new("reading-fluency", "Okuma akıcılığı", "Hız, akıcılık ve okuma ritmi çalışmaları.", 20),
        new("comprehension-and-strategy", "Anlama ve strateji", "Anlama, tarama ve sınav stratejisi çalışmaları.", 30),
        new("vocabulary-and-language", "Kelime ve dil", "Kelime bilgisi ve dil farkındalığı çalışmaları.", 40)
    ];

    public static IReadOnlyList<ExerciseTaxonomyCategory> GetCategories() => Categories;

    public static string ResolveCategoryKey(string engineType) =>
        ExerciseConfigurationRules.NormalizeEngineType(engineType) switch
        {
            "grid_interaction" or "motion_path" or "visual_expansion" or "attention_training" or "focus"
                => "attention-and-eye-movement",
            "text_stream" or "text_fade" or "word_highlight" or "free_reading"
                or "regression_reduction" or "subvocalization_reduction" or "adaptive_fluency"
                => "reading-fluency",
            "scan_find" or "scanning" or "skimming" or "reading_comprehension"
                or "exam_simulation" or "visualization"
                => "comprehension-and-strategy",
            "vocabulary_builder" or "error_analysis" => "vocabulary-and-language",
            _ => throw new ArgumentException("Egzersiz motoru için sınıflandırma bulunamadı.", nameof(engineType))
        };
}
