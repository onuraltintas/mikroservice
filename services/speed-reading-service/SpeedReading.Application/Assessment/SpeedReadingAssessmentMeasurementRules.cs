namespace SpeedReading.Application.Assessment;

/// <summary>
/// Keeps assessment levels bounded by both reading speed and comprehension.
/// Speed-only scores are not sufficient evidence of an advanced reading level.
/// </summary>
public static class SpeedReadingAssessmentMeasurementRules
{
    public static int CalculateLevel(decimal averageWpm, decimal averageComprehension)
    {
        var speedLevel = SpeedReadingLevelRules.CalculateSpeedLevel(averageWpm);
        var comprehensionLevel = SpeedReadingLevelRules.CalculateComprehensionLevel(averageComprehension);
        return Math.Min(speedLevel, comprehensionLevel);
    }
}
