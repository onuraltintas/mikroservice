using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;
internal static class CoachingStudyWorkReports
{
    internal sealed record Row(StudyPlanTask Task,StudyPlanStatus Status,bool WasPublished);
    internal static async Task<Row[]> ReadAsync(CoachingDbContext db,Guid student,DateOnly fromDate,DateOnly toDate,CancellationToken ct)
    {
        var rows=await (from task in db.StudyPlanTasks.AsNoTracking() join plan in db.StudyPlanRevisions.AsNoTracking() on task.RevisionId equals plan.Id
            where task.StudentId==student&&plan.StudentId==student
                && (task.PlannedDate>=fromDate&&task.PlannedDate<=toDate || task.WorkFromDate<=toDate&&task.WorkToDate>=fromDate)
                && (plan.Status==StudyPlanStatus.Active || plan.Status==StudyPlanStatus.Archived&&(plan.WasPublished||task.IsCompleted))
            orderby task.PlannedDate,task.Id
            select new Row(task,plan.Status,plan.WasPublished)).Take(10001).ToArrayAsync(ct);
        if(rows.Length>10000)throw new ArgumentException("Çalışma raporu sınırı aşıldı. Daha kısa bir dönem seçin.");
        return rows;
    }
    internal static StudentStudyReport Calculate(DateOnly fromDate,DateOnly toDate,IEnumerable<Row> rows)=>StudyReportCalculator.Calculate(fromDate,toDate,
        rows.Select(x=>new StudyReportTask(x.Status,x.Task.PlannedDate,x.Task.TopicId,x.Task.PlannedMinutes,x.Task.IsCompleted,
            x.Task.ActualMinutes,x.Task.PlannedQuestions,x.Task.ActualQuestions,x.WasPublished,x.Task.OriginalTaskId??x.Task.Id,x.Task.WorkDays)).ToArray());
}
