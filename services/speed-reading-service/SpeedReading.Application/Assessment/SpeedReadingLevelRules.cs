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
    public const string DefaultCatalogVersion = "tr-standard-v1";

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
        CalculateSpeedLevel(averageWpm, Definitions);

    public static int CalculateSpeedLevel(
        decimal averageWpm,
        IReadOnlyList<SpeedReadingLevelDefinition> definitions) =>
        FindLevel(Math.Max(0, averageWpm), definitions, item => item.MinimumWpm);

    public static int CalculateComprehensionLevel(decimal averageComprehension) =>
        CalculateComprehensionLevel(averageComprehension, Definitions);

    public static int CalculateComprehensionLevel(
        decimal averageComprehension,
        IReadOnlyList<SpeedReadingLevelDefinition> definitions) =>
        FindLevel(Math.Clamp(averageComprehension, 0, 100), definitions, item => item.MinimumComprehension);

    public static string GetDisplayName(int level) =>
        GetDisplayName(level, Definitions);

    public static string GetDisplayName(
        int level,
        IReadOnlyList<SpeedReadingLevelDefinition> definitions) =>
        definitions.FirstOrDefault(item => item.Level == level)?.DisplayName
        ?? $"Seviye {level}";

    public static void ValidateDefinitions(IReadOnlyList<SpeedReadingLevelDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (definitions.Count is < 2 or > 20)
            throw new ArgumentException("A level catalog must contain between 2 and 20 levels.", nameof(definitions));
        if (!definitions.Select(item => item.Level).SequenceEqual(Enumerable.Range(1, definitions.Count)))
            throw new ArgumentException("Level numbers must be contiguous and start at one.", nameof(definitions));
        if (definitions.Select(item => item.Code.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != definitions.Count)
            throw new ArgumentException("Level codes must be unique.", nameof(definitions));

        for (var index = 0; index < definitions.Count; index++)
        {
            var item = definitions[index];
            if (string.IsNullOrWhiteSpace(item.Code) || item.Code.Trim().Length > 50)
                throw new ArgumentException("Level codes are required and must not exceed 50 characters.", nameof(definitions));
            if (string.IsNullOrWhiteSpace(item.DisplayName) || item.DisplayName.Trim().Length > 100)
                throw new ArgumentException("Level names are required and must not exceed 100 characters.", nameof(definitions));
            if (item.MinimumWpm is < 0 or > 2000 || item.MinimumComprehension is < 0 or > 100)
                throw new ArgumentException("Level thresholds are outside their supported ranges.", nameof(definitions));
            if (index == 0 && (item.MinimumWpm != 0 || item.MinimumComprehension != 0))
                throw new ArgumentException("The first level must start at zero.", nameof(definitions));
            if (index > 0
                && (item.MinimumWpm <= definitions[index - 1].MinimumWpm
                    || item.MinimumComprehension <= definitions[index - 1].MinimumComprehension))
            {
                throw new ArgumentException("Level thresholds must increase strictly.", nameof(definitions));
            }
        }
    }

    private static int FindLevel(
        decimal value,
        IReadOnlyList<SpeedReadingLevelDefinition> definitions,
        Func<SpeedReadingLevelDefinition, int> selector)
    {
        ValidateDefinitions(definitions);
        var level = definitions
            .Where(item => value >= selector(item))
            .OrderBy(item => selector(item))
            .LastOrDefault();
        return level?.Level ?? definitions[0].Level;
    }
}
