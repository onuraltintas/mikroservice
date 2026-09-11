using SpeedReading.Domain.Assessment;

namespace SpeedReading.Application.Assessment;

public sealed record SpeedReadingCalibrationObservation(
    AssessmentAttemptPhase Phase,
    string LevelCatalogVersion,
    string AgeGroup,
    decimal Wpm,
    decimal Comprehension,
    string? StudyCode = null,
    string? ProtocolVersion = null,
    string? CohortCode = null);

public sealed record SpeedReadingCalibrationSegment(
    AssessmentAttemptPhase Phase,
    string LevelCatalogVersion,
    string AgeGroup,
    int SampleSize,
    decimal MeanWpm,
    decimal MedianWpm,
    decimal StandardDeviationWpm,
    decimal LowerQuartileWpm,
    decimal UpperQuartileWpm,
    decimal MeanComprehension,
    decimal MedianComprehension,
    bool IsPublishable,
    string EvidenceStatus,
    bool StudentIdentifiersExposed = false,
    string? StudyCode = null,
    string? ProtocolVersion = null,
    string? CohortCode = null);

public sealed record SpeedReadingCalibrationReport(
    bool DataAvailable,
    string? UnavailableReason,
    int MinimumPublishableSampleSize,
    DateTime GeneratedAt,
    IReadOnlyList<SpeedReadingCalibrationSegment> Segments);

public interface ISpeedReadingCalibrationAnalytics
{
    Task<SpeedReadingCalibrationReport> GetAsync(CancellationToken cancellationToken);
}

public sealed class UnavailableSpeedReadingCalibrationAnalytics : ISpeedReadingCalibrationAnalytics
{
    public Task<SpeedReadingCalibrationReport> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new SpeedReadingCalibrationReport(
            false,
            "Versioned calibration analytics require owned Speed Reading assessment data.",
            SpeedReadingCalibrationRules.MinimumPublishableSampleSize,
            DateTime.UtcNow,
            []));
}

public static class SpeedReadingCalibrationRules
{
    public const int MinimumPublishableSampleSize = 30;

    public static IReadOnlyList<SpeedReadingCalibrationSegment> Summarize(
        IReadOnlyCollection<SpeedReadingCalibrationObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        return observations
            .Where(item => item.Wpm >= 0 && item.Comprehension is >= 0 and <= 100)
            .GroupBy(item => new
            {
                item.Phase,
                item.LevelCatalogVersion,
                item.AgeGroup,
                item.StudyCode,
                item.ProtocolVersion,
                item.CohortCode
            })
            .Select(group =>
            {
                var wpm = group.Select(item => item.Wpm).Order().ToArray();
                var comprehension = group.Select(item => item.Comprehension).Order().ToArray();
                var publishable = group.Count() >= MinimumPublishableSampleSize;
                return new SpeedReadingCalibrationSegment(
                    group.Key.Phase,
                    group.Key.LevelCatalogVersion,
                    group.Key.AgeGroup,
                    group.Count(),
                    Round(wpm.Average()),
                    Percentile(wpm, 0.5m),
                    StandardDeviation(wpm),
                    Percentile(wpm, 0.25m),
                    Percentile(wpm, 0.75m),
                    Round(comprehension.Average()),
                    Percentile(comprehension, 0.5m),
                    publishable,
                    publishable ? "Publishable" : "PilotOnly",
                    false,
                    group.Key.StudyCode,
                    group.Key.ProtocolVersion,
                    group.Key.CohortCode);
            })
            .OrderBy(item => item.Phase)
            .ThenBy(item => item.LevelCatalogVersion)
            .ThenBy(item => item.AgeGroup)
            .ToList();
    }

    private static decimal Percentile(decimal[] sorted, decimal percentile)
    {
        if (sorted.Length == 0) return 0;
        var position = (sorted.Length - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper) return Round(sorted[lower]);
        return Round(sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower));
    }

    private static decimal StandardDeviation(decimal[] values)
    {
        if (values.Length < 2) return 0;
        var mean = values.Average();
        var variance = values.Sum(value => (value - mean) * (value - mean)) / (values.Length - 1);
        return Round((decimal)Math.Sqrt((double)variance));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
