using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace Coaching.Infrastructure.StudyPlanning;
internal static class CoachingWeeklyWorkReader
{
    internal static WeeklyDecisionView View(WeeklyDecision x,bool editable,DateOnly today,bool includeOrigin=true)=>new(x.Id,x.Version,x.ReviewWeekStart,x.Title,x.Note,x.DueOn,
        x.Status,x.CompletedAt,x.DueOn<today&&x.Status is ("Planned" or "InProgress"),editable,x.History,
        new(x.StudyTaskId is {} task?Missing(task):null,x.AssignmentId is {} assignment?Missing(assignment):null),x.TeacherId,x.ResponsibleTeacherId,x.InitialDueOn,
        !includeOrigin||x.SourceReflectionSnapshotJson is null?null:JsonSerializer.Deserialize<WeeklyDecisionSourceView>(x.SourceReflectionSnapshotJson),x.PreviousDecisionId,
        !includeOrigin||x.PreviousDecisionSnapshotJson is null?null:JsonSerializer.Deserialize<WeeklyResultDecision>(x.PreviousDecisionSnapshotJson));
    internal static DecisionLinkedWork Missing(Guid id) => new(id,"İlgili kayda artık erişilemiyor.","Unavailable",false,false,false);
    internal static async Task<WeeklyDecisionView[]> EnrichAsync(Coaching.Infrastructure.Data.CoachingDbContext db,TimeProvider clock,WeeklyDecisionView[] views, Guid student, CancellationToken ct)
    {
        var taskIds = views.Where(x=>x.Work?.Study is not null).Select(x=>x.Work!.Study!.Id).ToArray();
        var assignmentIds = views.Where(x=>x.Work?.Assignment is not null).Select(x=>x.Work!.Assignment!.Id).ToArray();
        if(taskIds.Length==0&&assignmentIds.Length==0)return views;
        var tasks = await (from task in db.StudyPlanTasks.AsNoTracking() join plan in db.StudyPlanRevisions.AsNoTracking() on task.RevisionId equals plan.Id
            where task.StudentId==student && plan.StudentId==student && Enumerable.Contains(taskIds,task.Id)
            select new { Task=task, Plan=plan }).ToDictionaryAsync(x=>x.Task.Id,ct);
        var roots=tasks.Values.Select(x=>x.Task.OriginalTaskId ?? x.Task.Id).Distinct().ToArray();
        var active=await (from task in db.StudyPlanTasks.AsNoTracking() join plan in db.StudyPlanRevisions.AsNoTracking() on task.RevisionId equals plan.Id
            where task.StudentId==student && plan.StudentId==student && plan.IsActive && Enumerable.Contains(roots,task.OriginalTaskId ?? task.Id)
            select new {Task=task,Plan=plan}).ToArrayAsync(ct);
        var assignments = await db.AssignmentStudents.AsNoTracking().Include(x=>x.Assignment)
            .Where(x=>x.StudentId==student && Enumerable.Contains(assignmentIds,x.AssignmentId)).ToDictionaryAsync(x=>x.AssignmentId,ct);
        var now=clock.GetUtcNow();
        return views.Select(view=> {
            var study=view.Work?.Study;var assignment=view.Work?.Assignment;
            if(study is not null && tasks.TryGetValue(study.Id,out var found)) {
                var continuations=active.Where(x=>(x.Task.OriginalTaskId ?? x.Task.Id)==(found.Task.OriginalTaskId ?? found.Task.Id)).ToArray();
                if(continuations.Length>1)throw new EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException("WeeklyReview.Conflict","Çalışmanın devam bağlantısı belirsiz.");
                if(view.Status is ("Planned" or "InProgress") && found.Plan.Status==StudyPlanStatus.Archived && continuations.Length==1)found=continuations[0];
                var task=found.Task;var zone=TimeZoneInfo.FindSystemTimeZoneById(found.Plan.WeeklySource?.TimeZoneId??"Europe/Istanbul");
                var local=TimeZoneInfo.ConvertTime(now,zone);var today=DateOnly.FromDateTime(local.DateTime);
                study=new(task.Id,task.Title,task.IsCompleted?"Completed":found.Plan.Status==StudyPlanStatus.Archived?"Archived":"Pending",task.IsCompleted,
                    !task.IsCompleted && (task.PlannedDate<today || task.PlannedDate==today && task.PlannedStartMinute+task.PlannedMinutes<=local.Hour*60+local.Minute),
                    true,task.RevisionId,task.PlannedDate,task.PlannedStartMinute,task.PlannedMinutes,task.ActualMinutes,ActualQuestions:task.ActualQuestions,CompletedAt:task.CompletedAt,ResultRevision:CoachingReviewFollowUp.StudyRevision(task));
            }
            if(assignment is not null && assignments.TryGetValue(assignment.Id,out var row)) {
                var cancelled=row.Assignment.Status==AssignmentStatus.Cancelled;var done=!cancelled && row.Status is StudentAssignmentStatus.Submitted or StudentAssignmentStatus.Graded;
                assignment=new(row.AssignmentId,row.Assignment.Title,cancelled?"Cancelled":row.Status.ToString(),done,!cancelled&&!done&&row.Assignment.DueDate<now.UtcDateTime,
                    !cancelled,Date:DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(row.Assignment.DueDate,TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"))),Score:row.Score,Feedback:row.TeacherFeedback,SubmittedAt:row.SubmittedAt,ResultRevision:row.GradeRevision);
            }
            return view with { Work=new(study,assignment) };
        }).ToArray();
    }
}
