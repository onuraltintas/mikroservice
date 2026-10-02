using Coaching.Domain.Enums;

namespace Coaching.Application.StudyPlanning;

public sealed record GoalScoreEvidence(Guid ResultId, Guid ExamId, string Source, ExamType ExamType,
    decimal MaxScore, decimal Score, DateTime ExamDate);
public sealed record GoalScoreComparison(Guid ResultId, Guid ExamId, string Source, DateTime ExamDate,
    decimal Score, decimal TargetAttainmentPercentage, decimal RemainingScore, bool TargetReached);
public sealed record GoalScoreAssessment(string Reason, IReadOnlyList<GoalScoreComparison> Comparisons);

public static class GoalScoreCalculator
{
    public static GoalScoreAssessment Calculate(decimal? targetScore, decimal? maxScore,
        ExamType? examType, string? targetSubject, IReadOnlyList<GoalScoreEvidence> evidence)
    {
        if (targetScore is null or <= 0 || maxScore is null or <= 0 or > 999.99m
            || targetScore > maxScore || examType is null || !Enum.IsDefined(examType.Value))
            return new("ScoreTargetNotConfigured", []);
        // A named subject goal cannot be compared with a whole-exam score.
        if (!string.IsNullOrWhiteSpace(targetSubject)) return new("SubjectTargetNotComparable", []);

        var comparisons = evidence.Where(x => x.ExamType == examType && x.MaxScore == maxScore
            && x.Score >= 0 && x.Score <= x.MaxScore && x.Source is "StudentReported" or "TeacherRecorded")
            .GroupBy(x => x.Source).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(x => x.ExamDate).ThenByDescending(x => x.ResultId).First())
            .Select(x => new GoalScoreComparison(x.ResultId, x.ExamId, x.Source, x.ExamDate, x.Score,
                decimal.Round(Math.Min(100m, 100m * x.Score / targetScore.Value), 1, MidpointRounding.AwayFromZero),
                Math.Max(0m, targetScore.Value - x.Score), x.Score >= targetScore.Value)).ToArray();
        return new(comparisons.Length == 0 ? "NoMatchingResults" : "LatestMatchingResultPerSource", comparisons);
    }
}
