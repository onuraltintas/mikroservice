using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

public sealed record PracticeEvidence(Guid ExamId, string Title, DateTime ExamDate, string Source, PracticeExamInput Input, DateTime? RecordedAt = null, PracticeTopicAnalysis? Analysis = null);
public sealed record PracticeNetPoint(Guid ExamId, string Title, DateTime ExamDate, decimal Net);
public sealed record PracticeSectionTrend(string Code, string Name, int QuestionCount, decimal AverageNet, decimal NetPercentage, bool NeedsReview);
public sealed record PracticeNetTrend(string ExamCode, string Name, string Source, IReadOnlyList<PracticeNetPoint> Points,
    decimal? Change, IReadOnlyList<PracticeSectionTrend> Sections);
public sealed record GoalNetComparison(string Source, DateTime ExamDate, decimal LatestNet, decimal RemainingNet);
public sealed record PracticeGoalView(Guid GoalId, string Title, string? ExamCode, decimal? TargetNet,
    string? CatalogName, int? CatalogYear, int? ScoreYear, string? PlacementStage, decimal? MinimumScore,
    int? Rank, decimal? Percentile, IReadOnlyList<GoalNetComparison> Comparisons);
public sealed record RecoveryProposal(Guid PlanId, int ExpectedVersion, IReadOnlyList<Guid> TaskIds, string Title,
    DateOnly PreviousDate, int PreviousStartMinute, DateOnly PlannedDate, int StartMinute, int Minutes);
public sealed record PracticeProgressView(DateOnly FromDate, DateOnly ToDate, DateOnly Today, string TimeZoneId,
    IReadOnlyList<PracticeNetTrend> Trends, IReadOnlyList<PracticeGoalView> Goals, int ScheduledTasks, int CompletedTasks,
    int OverdueTasks, int PinnedOverdueTasks, IReadOnlyList<RecoveryProposal> Recovery, string RecoveryReason);

public static class PracticeProgressCalculator
{
    public static IReadOnlyList<PracticeNetTrend> Calculate(IEnumerable<PracticeEvidence> evidence) => evidence
        .GroupBy(x => new { x.Input.ExamCode, x.Source }).Select(group => {
            var recent = group.OrderByDescending(x => x.ExamDate).ThenByDescending(x=>x.RecordedAt).ThenBy(x => x.ExamId).Take(6)
                .OrderBy(x => x.ExamDate).ThenBy(x=>x.RecordedAt).ThenBy(x => x.ExamId).ToArray();
            var summaries = recent.Select(x => PracticeExamRules.Summarize(x.Input)).ToArray();
            var profile = PracticeExamRules.Profiles.Single(x => x.Code == group.Key.ExamCode);
            var sections = profile.Sections.Select(section => {
                var rows = recent.Select(x => x.Input.Sections.Single(s => s.SectionCode == section.Code)).ToArray();
                var net = rows.Sum(x => x.Correct) - rows.Sum(x => x.Wrong) / (decimal)profile.WrongAnswerDivisor;
                var percentage = 100m * net / (rows.Length * section.QuestionCount);
                return new PracticeSectionTrend(section.Code, section.Name, section.QuestionCount,
                    decimal.Round(net / rows.Length, 2), decimal.Round(percentage, 1), percentage < 50);
            }).ToArray();
            return new PracticeNetTrend(profile.Code, profile.Name, group.Key.Source,
                recent.Select((x, i) => new PracticeNetPoint(x.ExamId, x.Title, x.ExamDate, summaries[i].Net)).ToArray(),
                recent.Length < 2 ? null : summaries[^1].Net - summaries[0].Net, sections);
        }).OrderBy(x => x.ExamCode).ThenBy(x => x.Source).ToArray();
}
