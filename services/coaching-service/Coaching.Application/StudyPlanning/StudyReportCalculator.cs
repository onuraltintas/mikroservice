using Coaching.Domain.Entities;
using Coaching.Domain.Enums;

namespace Coaching.Application.StudyPlanning;

public sealed record StudyReportTask(StudyPlanStatus Status, DateOnly PlannedDate, Guid? TopicId,
    int PlannedMinutes, bool IsCompleted, int? ActualMinutes, int? PlannedQuestions = null, int? ActualQuestions = null, bool WasPublished = false, Guid? WorkId = null, IReadOnlyList<StudyWorkDay>? WorkDays = null);
public sealed record StudyTopicReport(Guid? TopicId, int ScheduledTasks, int CompletedTasks,
    long PlannedMinutes, long? ActualMinutes, string? TopicName = null,
    long? PlannedQuestions = null, long? ActualQuestions = null, int PlannedQuestionTasks = 0, int ActualQuestionTasks = 0);
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
    public long? LegacyDateActualMinutes { get; init; }
    public long? PlannedQuestions { get; init; }
    public long? ActualQuestions { get; init; }
    public int PlannedQuestionTasks { get; init; }
    public int ActualQuestionTasks { get; init; }
    public IReadOnlyList<StudyLessonResult> LessonResults { get; init; } = [];
    public IReadOnlyList<StudyGoalReport> Goals { get; init; } = [];
    public string GoalReason { get; init; } = "CurrentGoalsWithRecordedProgress";
}

public static class StudyReportCalculator
{
    public static StudentStudyReport Calculate(DateOnly from, DateOnly to, IReadOnlyList<StudyReportTask> tasks)
    {
        ValidatePeriod(from, to);
        var reportable=tasks.Where(x=>x.Status==StudyPlanStatus.Active || x.Status==StudyPlanStatus.Archived&&(x.WasPublished||x.IsCompleted)).ToArray();
        // Current completion state of work scheduled in the period; this is not a historical as-of snapshot.
        var eligible = tasks.Where(x => x.PlannedDate >= from && x.PlannedDate <= to
            && (x.Status == StudyPlanStatus.Active || x.Status == StudyPlanStatus.Archived && (x.WasPublished || x.IsCompleted)))
            .Select((task, index) => new { Task = task, Unique = task.WorkId.HasValue ? -1 : index })
            .GroupBy(x => (x.Task.WorkId, x.Task.PlannedDate, x.Unique))
            .Select(g => {
                var rows = g.Select(x => x.Task).ToArray();
                return rows.OrderByDescending(x => x.IsCompleted).ThenByDescending(x => x.Status == StudyPlanStatus.Active).First()
                    with { PlannedMinutes = rows.Max(x=>x.PlannedMinutes),
                        PlannedQuestions = rows.Any(x=>x.PlannedQuestions.HasValue) ? rows.Where(x=>x.PlannedQuestions.HasValue).Max(x=>x.PlannedQuestions) : null, ActualMinutes = rows.Any(x => x.ActualMinutes.HasValue) ? checked((int)rows.Sum(x => (long)(x.ActualMinutes ?? 0))) : null,
                        ActualQuestions = rows.Any(x => x.ActualQuestions.HasValue) ? checked((int)rows.Sum(x => (long)(x.ActualQuestions ?? 0))) : null };
            }).ToArray();
        // Planned obligations stay in the scheduled period. Effort is independently grouped by its work date.
        var effort = reportable
            .SelectMany(x=>(x.WorkDays ?? (x.ActualMinutes is {} m ? [new StudyWorkDay(x.PlannedDate,m,x.ActualQuestions,true)] : Array.Empty<StudyWorkDay>()))
                .Where(d=>d.WorkedOn>=from&&d.WorkedOn<=to).Select(d=>new {x.TopicId,Day=d})).ToArray();
        var completed = eligible.Count(x=>x.IsCompleted);
        long? Minutes(IEnumerable<StudyWorkDay> rows) { var values=rows.ToArray();return values.Length==0 ? null : values.Sum(x=>(long)x.Minutes); }
        long? ActualQuestions(IEnumerable<StudyWorkDay> rows) { var values=rows.Where(x=>x.Questions.HasValue).ToArray();return values.Length==0?null:values.Sum(x=>(long)x.Questions!.Value); }
        var topics = eligible.Select(x=>x.TopicId).Union(effort.Select(x=>x.TopicId)).OrderBy(x=>x).Select(topic=> {
            var planned=eligible.Where(x=>x.TopicId==topic).ToArray();var days=effort.Where(x=>x.TopicId==topic).Select(x=>x.Day).ToArray();
            return new StudyTopicReport(topic,planned.Length,planned.Count(x=>x.IsCompleted),planned.Sum(x=>(long)x.PlannedMinutes),Minutes(days),
                PlannedQuestions:Questions(planned,false),ActualQuestions:ActualQuestions(days),PlannedQuestionTasks:planned.Count(x=>x.PlannedQuestions.HasValue),
                ActualQuestionTasks:reportable.Where(x=>x.TopicId==topic && (x.WorkDays??(x.ActualMinutes is {} m ? [new StudyWorkDay(x.PlannedDate,m,x.ActualQuestions,true)] : Array.Empty<StudyWorkDay>())).Any(d=>d.WorkedOn>=from&&d.WorkedOn<=to&&d.Questions.HasValue)).Select((x,index)=>(x.WorkId,Unique:x.WorkId.HasValue?-1:index)).Distinct().Count());
        }).ToArray();
        return new StudentStudyReport(from,to,"StudentReported",eligible.Length==0 ? "NoScheduledTasks" : "CurrentStatusOfScheduledWork",
            eligible.Length,completed,eligible.Length==0?null:decimal.Round(100m*completed/eligible.Length,1),
            eligible.Sum(x=>(long)x.PlannedMinutes),Minutes(effort.Select(x=>x.Day)),topics) {
                PlannedQuestions=Questions(eligible,false),ActualQuestions=ActualQuestions(effort.Select(x=>x.Day)),
                PlannedQuestionTasks=eligible.Count(x=>x.PlannedQuestions.HasValue),ActualQuestionTasks=topics.Sum(x=>x.ActualQuestionTasks),
                LegacyDateActualMinutes=Minutes(effort.Where(x=>x.Day.IsLegacyDate).Select(x=>x.Day)) };
    }

    public static void ValidatePeriod(DateOnly from, DateOnly to)
    {
        if (from == DateOnly.MinValue || to < from || to.DayNumber - from.DayNumber > 365)
            throw new ArgumentException("Choose an ordered period of at most 366 days.");
    }

    private static long? Actual(IEnumerable<StudyReportTask> tasks)
    {
        var recorded = tasks.Where(x => x.ActualMinutes.HasValue).ToArray();
        return recorded.Length == 0 ? null : recorded.Sum(x => (long)x.ActualMinutes!.Value);
    }
    private static long? Questions(IEnumerable<StudyReportTask> tasks, bool actual)
    {
        var recorded = tasks.Where(x => actual ? x.ActualQuestions.HasValue : x.PlannedQuestions.HasValue).ToArray();
        return recorded.Length == 0 ? null : recorded.Sum(x => (long)(actual ? x.ActualQuestions!.Value : x.PlannedQuestions!.Value));
    }
}
