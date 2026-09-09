namespace SpeedReading.Application.Assessment;

public sealed record SpeedReadingLevelDefinition(
    int Level,
    string Code,
    string DisplayName,
    int MinimumWpm,
    int MinimumComprehension);

/// <summary>
/// Single in-code level dictionary used by assessment placement and result labels.
/// The values are safe defaults until a versioned, normed catalog is persisted.
/// </summary>
public static class SpeedReadingLevelRules
{
    public static IReadOnlyList<SpeedReadingLevelDefinition> Definitions { get; } =
    [
        new(1, "beginner", "Başlangıç", 0, 0),
        new(2, "basic", "Temel", 100, 40),
        new(3, "lower_intermediate", "Orta-Alt", 150, 55),
        new(4, "intermediate", "Orta", 200, 65),
        new(5, "upper_intermediate", "Orta-Üst", 250, 70),
        new(6, "advanced", "İleri", 300, 75),
        new(7, "expert", "Uzman", 400, 80),
        new(8, "elite", "Elit", 500, 90)
    ];

    public static int CalculateSpeedLevel(decimal averageWpm) =>
        FindLevel(Math.Max(0, averageWpm), item => item.MinimumWpm);

    public static int CalculateComprehensionLevel(decimal averageComprehension) =>
        FindLevel(Math.Clamp(averageComprehension, 0, 100), item => item.MinimumComprehension);

    public static string GetDisplayName(int level) =>
        Definitions.FirstOrDefault(item => item.Level == level)?.DisplayName
        ?? $"Seviye {level}";

    private static int FindLevel(decimal value, Func<SpeedReadingLevelDefinition, int> selector)
    {
        var level = Definitions
            .Where(item => value >= selector(item))
            .OrderBy(item => selector(item))
            .LastOrDefault();
        return level?.Level ?? Definitions[0].Level;
    }
}
