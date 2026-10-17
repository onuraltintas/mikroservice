using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace Coaching.API.Controllers;

public sealed record TeacherStudyWork(Guid TaskId,Guid PlanId,string Title,DateOnly PlannedDate,int? PlannedStartMinute,int PlannedMinutes,bool IsCompleted,bool IsOverdue,int? ActualMinutes);
public sealed record TeacherStudyStudent(Guid StudentId,string FullName,bool HasActivePlan,bool HasDraft,
    int CompletedTasks,int PendingTasks,int OverdueTasks,int ActualMinutes,IReadOnlyList<TeacherStudyWork>? Tasks=null);
public sealed record TeacherStudyOverview(IReadOnlyList<TeacherStudyStudent> Items,int TotalCount,int PageNumber,int PageSize);

[ApiController,ApiVersion(1.0),Authorize(Roles="Teacher")]
[Route("api/coaching/teachers/me/study-overview")]
public sealed class CoachingTeacherStudyController(CoachingDbContext db,OwnedCoachingDirectory directory,
    ICurrentUserService actor,TimeProvider? clock=null):ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Overview([FromQuery,BindRequired] DateOnly fromDate,[FromQuery,BindRequired] DateOnly toDate,
        int pageNumber=1,int pageSize=25,string? search=null,CancellationToken ct=default)
    {
        try
        {
            if(actor.UserId is not {} teacher || !actor.Roles.Contains("Teacher",StringComparer.OrdinalIgnoreCase))return Forbid();
            StudyReportCalculator.ValidatePeriod(fromDate,toDate);
            var roster=await directory.GetTeacherRosterAsync(teacher,pageNumber,pageSize,search,null,true,null,ct);
            var students=roster.Items.DistinctBy(x=>x.UserId).ToArray();var ids=students.Select(x=>x.UserId).ToArray();
            var plans=await db.StudyPlanRevisions.AsNoTracking().Where(x=>Enumerable.Contains(ids,x.StudentId)).Select(x=>new{x.Id,x.StudentId,x.Status,x.WeeklySource}).ToArrayAsync(ct);
            var rows=await (from task in db.StudyPlanTasks.AsNoTracking() join plan in db.StudyPlanRevisions.AsNoTracking() on task.RevisionId equals plan.Id
                where Enumerable.Contains(ids,task.StudentId)&&plan.StudentId==task.StudentId&&task.PlannedDate>=fromDate&&task.PlannedDate<=toDate
                    &&(plan.Status==StudyPlanStatus.Active||plan.Status==StudyPlanStatus.Archived&&task.IsCompleted)
                select new{task.Id,task.Title,task.StudentId,task.RevisionId,task.PlannedDate,task.PlannedStartMinute,task.PlannedMinutes,task.IsCompleted,task.ActualMinutes}).Take(10001).ToArrayAsync(ct);
            if(rows.Length>10000)return UnprocessableEntity(new{message="Dönem çok fazla çalışma içeriyor. Daha kısa bir tarih aralığı seçin."});
            var now=(clock??TimeProvider.System).GetUtcNow();
            var localTimes=plans.ToDictionary(p=>p.Id,p=>TimeZoneInfo.ConvertTime(now,TimeZoneInfo.FindSystemTimeZoneById(p.WeeklySource?.TimeZoneId??"Europe/Istanbul")));
            var grouped=rows.ToLookup(x=>x.StudentId);
            var items=students.Select(student=>{var work=grouped[student.UserId].Select(t=>new TeacherStudyWork(t.Id,t.RevisionId,t.Title,t.PlannedDate,t.PlannedStartMinute,t.PlannedMinutes,t.IsCompleted,!t.IsCompleted&&IsOverdue(t.PlannedDate,t.PlannedStartMinute,t.PlannedMinutes,localTimes[t.RevisionId]),t.ActualMinutes)).ToArray();return new TeacherStudyStudent(student.UserId,student.FullName,
                plans.Any(p=>p.StudentId==student.UserId&&p.Status==StudyPlanStatus.Active),plans.Any(p=>p.StudentId==student.UserId&&p.Status==StudyPlanStatus.Draft),
                work.Count(t=>t.IsCompleted),work.Count(t=>!t.IsCompleted),work.Count(t=>t.IsOverdue),work.Sum(t=>t.ActualMinutes??0),work);}).ToArray();
            return Ok(new TeacherStudyOverview(items,roster.TotalCount,pageNumber,pageSize));
        }
        catch(BusinessRuleException ex) when(ex.Code.StartsWith("Authorization.",StringComparison.Ordinal)){return StatusCode(403,new{message=ex.Message});}
        catch(ArgumentException){return BadRequest(new{message="Tarih aralığını, aramayı ve sayfa bilgilerini kontrol edin."});}
    }
    private static bool IsOverdue(DateOnly date,int? start,int minutes,DateTimeOffset now)
    {
        var today=DateOnly.FromDateTime(now.DateTime);
        return date<today||date==today&&start.HasValue&&start.Value+minutes<=now.Hour*60+now.Minute;
    }
}
