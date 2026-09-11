namespace SpeedReading.Application.Assessment;

/// <summary>
/// Keeps assessment levels bounded by both reading speed and comprehension.
/// Speed-only scores are not sufficient evidence of an advanced reading level.
/// </summary>
public static class SpeedReadingAssessmentMeasurementRules
{
    public static int CalculateLevel(decimal averageWpm, decimal averageComprehension)
        => CalculateLevel(averageWpm, averageComprehension, SpeedReadingLevelRules.Definitions);

    public static int CalculateLevel(
        decimal averageWpm,
        decimal averageComprehension,
        IReadOnlyList<SpeedReadingLevelDefinition> definitions)
    {
        var speedLevel = SpeedReadingLevelRules.CalculateSpeedLevel(averageWpm, definitions);
        var comprehensionLevel = SpeedReadingLevelRules.CalculateComprehensionLevel(averageComprehension, definitions);
        return Math.Min(speedLevel, comprehensionLevel);
    }
}
