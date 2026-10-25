using System.Security.Cryptography;
using System.Text.Json;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Contracts.Events.Coaching;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed partial class CoachingWeeklyReviewService
{
    private Task<WeeklyDecisionView[]> WorkViewsAsync(WeeklyDecisionView[] views,Guid student,CancellationToken ct)
        =>CoachingWeeklyWorkReader.EnrichAsync(db,clock,views,student,ct);
    private async Task<WeeklyDecision> RequireDecisionAsync(Guid student,DateOnly week,Guid id,CancellationToken ct,bool tracking=false)
    {
        ValidateWeek(week);if(id==Guid.Empty)throw new ArgumentException("Kararı seçin.");
        await Require(student,true,ct);
        var query=db.Set<WeeklyDecision>().Where(x=>x.Id==id&&x.StudentId==student&&x.ReviewWeekStart==week&&x.ResponsibleTeacherId==access.CurrentUserId);
        return await (tracking?query:query.AsNoTracking()).SingleOrDefaultAsync(ct)??throw Forbidden();
    }
    private Task<WeeklyDecisionView> WriteWorkAsync(Guid student,DateOnly week,Guid id,Func<WeeklyDecision,Task> action,CancellationToken ct)
        =>Write(async()=>{
            await RequireDecisionAsync(student,week,id,ct);await Lock(student,ct);
            var row=await RequireDecisionAsync(student,week,id,ct,true);await action(row);
            return (await WorkViewsAsync([View(row,true)],student,ct))[0];
        },(value,token)=>db.Set<WeeklyDecision>().AsNoTracking().AnyAsync(x=>x.Id==id&&x.StudentId==student&&x.Version==value.Version,token),ct);
    private static void Version(WeeklyDecision row,int expected){if(expected<0||row.Version!=expected)throw Conflict();}
    private async Task ChangedWorkAsync(WeeklyDecision row,CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        await events.PublishAsync(new WeeklyDecisionChangedEvent(row.Id,row.StudentId,row.ResponsibleTeacherId,row.ReviewWeekStart,false),ct);
        await db.SaveChangesAsync(ct);
    }
    public Task<WeeklyDecisionView> SaveLinksAsync(Guid student,DateOnly week,Guid id,DecisionLinksSave r,CancellationToken ct)
        =>WriteWorkAsync(student,week,id,async row=>{
            Version(row,r.ExpectedVersion);if(r.StudyTaskId==Guid.Empty||r.AssignmentId==Guid.Empty)throw new ArgumentException("Geçerli iş seçin.");
            if(row.StudyTaskId==r.StudyTaskId&&row.AssignmentId==r.AssignmentId)return;
            if(r.StudyTaskId.HasValue && row.StudyTaskId!=r.StudyTaskId && !await (from task in db.StudyPlanTasks join plan in db.StudyPlanRevisions on task.RevisionId equals plan.Id
                where task.Id==r.StudyTaskId&&task.StudentId==student&&plan.StudentId==student&&plan.Status==StudyPlanStatus.Active select task.Id).AnyAsync(ct))throw Forbidden();
            if(r.AssignmentId.HasValue && row.AssignmentId!=r.AssignmentId) {
                var institution=await directory.AuthorizeTeacherTargetsAsync(row.ResponsibleTeacherId,[student],null,false,ct);
                if(!await db.AssignmentStudents.AnyAsync(x=>x.AssignmentId==r.AssignmentId&&x.StudentId==student&&x.Assignment.TeacherId==row.ResponsibleTeacherId
                    &&x.Assignment.InstitutionId==institution&&x.Assignment.Status!=AssignmentStatus.Cancelled,ct))throw Forbidden();
            }
            row.LinkWork(r.StudyTaskId,r.AssignmentId,clock.GetUtcNow().UtcDateTime);await ChangedWorkAsync(row,ct);
        },ct);
    public async Task<DecisionWorkOptions> WorkOptionsAsync(Guid student,DateOnly week,Guid id,string kind,string? search,int page,CancellationToken ct)
    {
        var row=await RequireDecisionAsync(student,week,id,ct);if(kind is not("study" or "assignment")||page is <1 or >1000||search?.Length>200)throw new ArgumentException("Filtreleri kontrol edin.");
        var plan=await db.StudyPlanRevisions.AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==student&&x.IsActive,ct);
        DecisionWorkPlan? context=plan is null?null:new(plan.Id,plan.Version,plan.WeeklySource?.StartDate,plan.WeeklySource?.EndDate,plan.WeeklySource?.TimeZoneId??"Europe/Istanbul");
        var term=search?.Trim()??"";DecisionLinkedWork[] items;int count;
        if(kind=="study"){
            var planId=plan?.Id??Guid.Empty;
            var query=db.StudyPlanTasks.AsNoTracking().Where(x=>x.StudentId==student&&x.RevisionId==planId&&(term==""||EF.Functions.ILike(x.Title,"%"+term+"%")));
            count=await query.CountAsync(ct);items=await query.OrderBy(x=>x.PlannedDate).ThenBy(x=>x.PlannedStartMinute).ThenBy(x=>x.Id).Skip((page-1)*20).Take(20)
                .Select(x=>new DecisionLinkedWork(x.Id,x.Title,x.IsCompleted?"Completed":"Pending",x.IsCompleted,false,true,x.RevisionId,x.PlannedDate,x.PlannedStartMinute,x.PlannedMinutes,x.ActualMinutes,null)).ToArrayAsync(ct);
        }else{
            var institution=await directory.AuthorizeTeacherTargetsAsync(row.ResponsibleTeacherId,[student],null,false,ct);
            var query=db.AssignmentStudents.AsNoTracking().Where(x=>x.StudentId==student&&x.Assignment.TeacherId==row.ResponsibleTeacherId&&x.Assignment.InstitutionId==institution
                &&x.Assignment.Status!=AssignmentStatus.Cancelled&&(term==""||EF.Functions.ILike(x.Assignment.Title,"%"+term+"%")));
            count=await query.CountAsync(ct);items=(await query.Include(x=>x.Assignment).OrderByDescending(x=>x.Assignment.DueDate).ThenBy(x=>x.AssignmentId).Skip((page-1)*20).Take(20).ToArrayAsync(ct))
                .Select(x=>new DecisionLinkedWork(x.AssignmentId,x.Assignment.Title,x.Status.ToString(),x.Status is StudentAssignmentStatus.Submitted or StudentAssignmentStatus.Graded,false,true,Score:x.Score)).ToArray();
        }
        return new(items,count,page,context);
    }
    public async Task<DecisionStudyPreview> PreviewDecisionStudyAsync(Guid student,DateOnly week,Guid id,DecisionStudyProposal r,CancellationToken ct)
    {
        var row=await RequireDecisionAsync(student,week,id,ct);return await StudyPreviewAsync(row,r,ct);
    }
    private async Task<DecisionStudyPreview> StudyPreviewAsync(WeeklyDecision row,DecisionStudyProposal r,CancellationToken ct)
    {
        Version(row,r.ExpectedDecisionVersion);
        var plan=await db.StudyPlanRevisions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==r.PlanId&&x.StudentId==row.StudentId&&x.IsActive,ct)??throw Forbidden();
        if(r.ExpectedPlanVersion<0||plan.Version!=r.ExpectedPlanVersion)throw Conflict();
        var zone=plan.WeeklySource?.TimeZoneId??"Europe/Istanbul";var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime);
        if(r.Date<today||r.Minutes is <1 or >1440||r.StartMinute is <0 or >1439||r.StartMinute+r.Minutes>1440
            ||plan.WeeklySource is {} source&&(r.Date<source.StartDate||r.Date>source.EndDate))throw new ArgumentException("Çalışma tarihini, plan dönemini ve saat aralığını kontrol edin.");
        StudyPlanTask? task=null;
        if(r.TaskId.HasValue){
            if(r.TaskId!=row.StudyTaskId) {
                var linkedSource=await db.StudyPlanTasks.AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==row.StudentId&&x.Id==row.StudyTaskId,ct);
                if(linkedSource is null || !await db.StudyPlanTasks.AnyAsync(x=>x.StudentId==row.StudentId&&x.RevisionId==plan.Id&&x.Id==r.TaskId
                    &&(x.OriginalTaskId ?? x.Id)==(linkedSource.OriginalTaskId ?? linkedSource.Id),ct))throw Forbidden();
            }
            task=await db.StudyPlanTasks.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==r.TaskId&&x.RevisionId==plan.Id&&x.StudentId==row.StudentId,ct)??throw Forbidden();
            if(task.IsCompleted)throw Conflict();
            if(task.PlannedMinutes!=r.Minutes)throw new ArgumentException("Taşıma sırasında çalışma süresi korunmalıdır.");
        }
        var others=await db.StudyPlanTasks.AsNoTracking().Where(x=>x.RevisionId==plan.Id&&x.StudentId==row.StudentId&&x.PlannedDate==r.Date&&x.Id!=r.TaskId)
            .Select(x=>new{x.Id,x.PlannedStartMinute,x.PlannedMinutes,x.IsCompleted,x.Version}).ToArrayAsync(ct);
        if(others.Any(x=>x.PlannedStartMinute<r.StartMinute+r.Minutes&&x.PlannedStartMinute+x.PlannedMinutes>r.StartMinute))throw new ArgumentException("Bu saat başka bir çalışmayla çakışıyor. Boş bir saat seçin.");
        var total=others.Sum(x=>x.PlannedMinutes)+r.Minutes;if(total>1440)throw new ArgumentException("Bir güne 24 saatten fazla çalışma eklenemez.");
        var fingerprint=Hash(new{Proposal=r,row.Title,TaskVersion=task?.Version,Others=others.OrderBy(x=>x.Id)});
        return new(r,fingerprint,task?.Title??row.Title,zone,task?.PlannedDate,task?.PlannedStartMinute,others.Length,total,others.Count(x=>!x.PlannedStartMinute.HasValue));
    }
    public Task<WeeklyDecisionView> ApplyDecisionStudyAsync(Guid student,DateOnly week,Guid id,DecisionStudyApply r,CancellationToken ct)
        =>WriteWorkAsync(student,week,id,async row=>{
            if(!r.Confirmed||r.RequestId==Guid.Empty||string.IsNullOrEmpty(r.Fingerprint))throw new ArgumentException("Önizlemeyi inceleyip onaylayın.");
            var hash=Hash(new{student,week,id,Request=r});var key=id.ToString("D")+":"+r.RequestId.ToString("N");
            if(await ReplayAsync(row,"coaching.weekly-decision.study",key,hash,true,ct))return;
            var preview=await StudyPreviewAsync(row,r.Proposal,ct);if(preview.Fingerprint!=r.Fingerprint)throw Conflict();
            var plan=await db.StudyPlanRevisions.SingleAsync(x=>x.Id==r.Proposal.PlanId&&x.StudentId==student,ct);
            StudyPlanTask task;
            if(r.Proposal.TaskId is {} taskId){task=await db.StudyPlanTasks.SingleAsync(x=>x.Id==taskId&&x.StudentId==student,ct);task.Schedule(r.Proposal.Date,r.Proposal.StartMinute);}
            else{task=StudyPlanTask.Create(plan,r.Proposal.Date,row.Title,r.Proposal.Minutes);task.Schedule(r.Proposal.Date,r.Proposal.StartMinute);db.Add(task);}
            plan.RecordScheduleChange();row.LinkWork(task.Id,row.AssignmentId,clock.GetUtcNow().UtcDateTime);
            if(r.Proposal.TaskId.HasValue)row.RecordWorkChange(clock.GetUtcNow().UtcDateTime);
            db.Add(IdempotencyRecord.Create("coaching.weekly-decision.study",key,hash,task.Id));
            await events.PublishAsync(new StudyPlanUpdatedEvent(plan.Id,student,r.Proposal.TaskId.HasValue?"Haftalık karara bağlı çalışmanın takvim zamanı güncellendi.":"Haftalık karardan onaylanan yeni çalışma plana eklendi."),ct);
            await ChangedWorkAsync(row,ct);
        },ct);
    public Task<WeeklyDecisionView> CreateDecisionAssignmentAsync(Guid student,DateOnly week,Guid id,DecisionAssignmentCreate r,CancellationToken ct)
        =>WriteWorkAsync(student,week,id,async row=>{
            if(r.RequestId==Guid.Empty||r.ExpectedVersion<0||r.EstimatedMinutes is <1 or >1440)throw new ArgumentException("Ödev bilgilerini kontrol edin.");
            var hash=Hash(new{student,week,id,Request=r});var key=id.ToString("D")+":"+r.RequestId.ToString("N");
            if(await ReplayAsync(row,"coaching.weekly-decision.assignment",key,hash,false,ct))return;
            Version(row,r.ExpectedVersion);
            if(r.DueAt.Kind!=DateTimeKind.Utc||r.DueAt<=clock.GetUtcNow().UtcDateTime)throw new ArgumentException("Ödevin son teslimi gelecekte olmalıdır.");
            var institution=await directory.AuthorizeTeacherTargetsAsync(row.ResponsibleTeacherId,[student],null,false,ct);
            var assignment=Assignment.Create(row.ResponsibleTeacherId,row.Title,r.DueAt,institutionId:institution);assignment.UpdateDetails(description:row.Note,estimatedDurationMinutes:r.EstimatedMinutes);assignment.AssignToStudents([student]);db.Add(assignment);
            row.LinkWork(row.StudyTaskId,assignment.Id,clock.GetUtcNow().UtcDateTime);db.Add(IdempotencyRecord.Create("coaching.weekly-decision.assignment",key,hash,assignment.Id));
            await events.PublishAsync(new AssignmentCreatedEvent(assignment.Id,row.ResponsibleTeacherId,institution,assignment.Title,assignment.DueDate,[student]),ct);
            await ChangedWorkAsync(row,ct);
        },ct);
    private async Task<bool> ReplayAsync(WeeklyDecision row,string scope,string key,string hash,bool study,CancellationToken ct)
    {
        var old=await db.Set<IdempotencyRecord>().AsNoTracking().SingleOrDefaultAsync(x=>x.Scope==scope&&x.Key==key,ct);
        if(old is null)return false;if(!old.Matches(hash)||(study?row.StudyTaskId:row.AssignmentId)!=old.ResourceId)throw Conflict();return true;
    }
    private static string Hash<T>(T value)=>Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
