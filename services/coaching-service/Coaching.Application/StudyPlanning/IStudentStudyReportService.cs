namespace Coaching.Application.StudyPlanning;

public interface IStudentStudyReportService
{
    Task<StudentStudyReport> GetAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
    /// <summary>T7: totals for consecutive 7-day weeks starting at <paramref name="fromDate"/>, read in one query.</summary>
    Task<IReadOnlyList<StudyWeekTotals>> GetWeeksAsync(DateOnly fromDate, int weeks, CancellationToken cancellationToken = default);
}

public sealed record StudyWeekTotals(DateOnly FromDate, DateOnly ToDate, int ScheduledTasks, int CompletedTasks, long? ActualMinutes);
