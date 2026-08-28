using SpeedReading.Domain.Assessment;

namespace SpeedReading.Application.Assessment;

public sealed record AssessmentComparisonResultInput(
    bool IsMeasured,
    decimal? RawWpm,
    decimal? ComprehensionScore,
    decimal? Score,
    string Role);

public sealed record AssessmentComparisonAttemptInput(
    Guid AttemptId,
    AssessmentAttemptPhase Phase,
    AssessmentAttemptStatus Status,
    string FormVersion,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int ExpectedExerciseCount,
    IReadOnlyList<AssessmentComparisonResultInput> Results);

public sealed record AssessmentComparisonPoint(
    Guid AttemptId,
    AssessmentAttemptPhase Phase,
    AssessmentAttemptStatus Status,
    string FormVersion,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int ExpectedExerciseCount,
    int CompletedExerciseCount,
    decimal? AverageWpm,
    decimal? AverageComprehension,
    decimal? AverageScore,
    decimal? WpmDeltaFromBaseline,
    decimal? ComprehensionDeltaFromBaseline);

public sealed record AssessmentComparisonSummary(
    IReadOnlyList<AssessmentComparisonPoint> Attempts,
    AssessmentComparisonPoint? Baseline);

public static class AssessmentComparisonCalculator
{
    public static IReadOnlyList<AssessmentComparisonPoint> Calculate(
        IReadOnlyList<AssessmentComparisonAttemptInput>? attempts)
    {
        if (attempts is null || attempts.Count == 0)
            return [];

        var points = attempts
            .Where(item => item.Status == AssessmentAttemptStatus.Completed)
            .OrderBy(item => item.StartedAt)
            .Select(CreatePoint)
            .ToList();
        var baseline = points.FirstOrDefault(item => item.Phase == AssessmentAttemptPhase.Baseline);

        return points.Select(point => point with
        {
            WpmDeltaFromBaseline = point.Phase == AssessmentAttemptPhase.Baseline
                ? null
                : CalculateDelta(point.AverageWpm, baseline?.AverageWpm),
            ComprehensionDeltaFromBaseline = point.Phase == AssessmentAttemptPhase.Baseline
                ? null
                : CalculateDelta(point.AverageComprehension, baseline?.AverageComprehension)
        }).ToList();
    }

    private static AssessmentComparisonPoint CreatePoint(AssessmentComparisonAttemptInput attempt)
    {
        var measured = attempt.Results.Where(item => item.IsMeasured).ToList();
        var comprehensionValues = measured
            .Where(item => string.Equals(item.Role, "comprehension", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.ComprehensionScore)
            .Where(item => item.HasValue)
            .Select(item => Math.Clamp(item!.Value, 0, 100))
            .ToList();
        if (comprehensionValues.Count == 0)
        {
            comprehensionValues = measured
                .Select(item => item.ComprehensionScore)
                .Where(item => item.HasValue)
                .Select(item => Math.Clamp(item!.Value, 0, 100))
                .ToList();
        }

        return new AssessmentComparisonPoint(
            attempt.AttemptId,
            attempt.Phase,
            attempt.Status,
            attempt.FormVersion,
            attempt.StartedAt,
            attempt.CompletedAt,
            attempt.ExpectedExerciseCount,
            measured.Count,
            Average(measured
                .Select(item => item.RawWpm)
                .Where(item => item is > 0)
                .Select(item => item!.Value)),
            Average(comprehensionValues),
            Average(measured
                .Select(item => item.Score)
                .Where(item => item.HasValue)
                .Select(item => Math.Clamp(item!.Value, 0, 100))),
            null,
            null);
    }

    private static decimal? Average(IEnumerable<decimal> values)
    {
        var materialized = values.ToArray();
        return materialized.Length == 0
            ? null
            : Math.Round(materialized.Average(), 1);
    }

    private static decimal? CalculateDelta(decimal? value, decimal? baseline) =>
        value.HasValue && baseline.HasValue
            ? Math.Round(value.Value - baseline.Value, 1)
            : null;
}
