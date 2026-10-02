using Coaching.Domain.Entities;
using Coaching.Domain.Enums;

namespace Coaching.Application.StudyPlanning;

public sealed record StudyReportTask(StudyPlanStatus Status, DateOnly PlannedDate, Guid? TopicId,
    int PlannedMinutes, bool IsCompleted, int? ActualMinutes);
public sealed record StudyTopicReport(Guid? TopicId, int ScheduledTasks, int CompletedTasks,
    long PlannedMinutes, long? ActualMinutes, string? TopicName = null);
public sealed record StudyExamGroup(string Source, ExamType ExamType, decimal MaxScore, int Count, decimal AveragePercentage);
public sealed record StudyLessonResult(string Source, ExamType ExamType, Guid LessonId, Guid? TopicId,
    string? LessonName, string? TopicName, long QuestionCount, long Correct, long Wrong, long Empty);
public sealed record StudyGoalReport(Guid GoalId, string Title, string Source, int RecordedProgress,
    bool IsCompleted, DateTime? TargetDate, decimal? TargetScore, ExamType? TargetExamType, string? TargetSubject,
    decimal? TargetMaxScore = null)
{
    public GoalScoreAssessment ScoreAssessment { get; init; } = new("ScoreTargetNotConfigured", []);
}
public sealed record StudentStudyReport(DateOnly FromDate, DateOnly ToDate, string Source, string Reason,
    int ScheduledTasks, int CompletedTasks, decimal? CompletionPercentage, long PlannedMinutes,
    long? ActualMinutes, IReadOnlyList<StudyTopicReport> Topics)
{
    public IReadOnlyList<StudyExamGroup> ExamGroups { get; init; } = [];
    public IReadOnlyList<StudyLessonResult> LessonResults { get; init; } = [];
    public IReadOnlyList<StudyGoalReport> Goals { get; init; } = [];
    public string GoalReason { get; init; } = "CurrentGoalsWithRecordedProgress";
}

public static class StudyReportCalculator
{
    public static StudentStudyReport Calculate(DateOnly from, DateOnly to, IReadOnlyList<StudyReportTask> tasks)
    {
        ValidatePeriod(from, to);
        // Current completion state of work scheduled in the period; this is not a historical as-of snapshot.
        var eligible = tasks.Where(x => x.PlannedDate >= from && x.PlannedDate <= to
            && (x.Status == StudyPlanStatus.Active || x.Status == StudyPlanStatus.Archived && x.IsCompleted)).ToArray();
        var completed = eligible.Count(x => x.IsCompleted);
        var topics = eligible.GroupBy(x => x.TopicId).OrderBy(x => x.Key)
            .Select(g => new StudyTopicReport(g.Key, g.Count(), g.Count(x => x.IsCompleted),
                g.Sum(x => (long)x.PlannedMinutes), Actual(g))).ToArray();
        return new(from, to, "StudentReported", eligible.Length == 0 ? "NoScheduledTasks" : "CurrentStatusOfScheduledWork",
            eligible.Length, completed, eligible.Length == 0 ? null : decimal.Round(100m * completed / eligible.Length, 1),
            eligible.Sum(x => (long)x.PlannedMinutes), Actual(eligible), topics);
    }

    public static void ValidatePeriod(DateOnly from, DateOnly to)
    {
        if (from == DateOnly.MinValue || to < from || to.DayNumber - from.DayNumber > 365)
            throw new ArgumentException("Choose an ordered period of at most 366 days.");
    }

    private static long? Actual(IEnumerable<StudyReportTask> tasks)
    {
        var recorded = tasks.Where(x => x.IsCompleted && x.ActualMinutes.HasValue).ToArray();
        return recorded.Length == 0 ? null : recorded.Sum(x => (long)x.ActualMinutes!.Value);
    }
}
