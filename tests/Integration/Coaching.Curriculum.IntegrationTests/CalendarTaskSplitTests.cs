using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Contracts.Events.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

/// <summary>Paket H5: a Planım task is split into two consecutive halves the student can move on their own.</summary>
public sealed class CalendarTaskSplitTests
{
    private static CoachingManualStudyPlanService Student(CurriculumManagementTests.Fixture f,Guid student)
    {
        f.User.UserId=student;f.User.Roles=["Student"];var policy=new CoachingAccessPolicy(f.User);
        return new(f.Db,policy,new CoachingAutomaticStudyPlanPreviewService(f.Db,policy),new CurriculumManagementTests.NoopPublisher());
    }

    private static async Task<(StudyCatalogOutcome Second,OutcomeDurationEstimate Estimate)> SecondOutcome(CurriculumManagementTests.Fixture f)
    {
        var first=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();
        var outcome=StudyCatalogOutcome.Create(first.EditionId,first.OfficialTopicId,"M.8.1.1.2","İkinci kazanım","",74,2);
        var estimate=OutcomeDurationEstimate.Create(outcome.Id,1,20,30,10,"ReviewedStudy","Medium","İkinci süre dayanağı",f.User.UserId);estimate.Publish();
        f.Db.AddRange(outcome,estimate);await f.Db.SaveChangesAsync();return (outcome,estimate);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task SplitsAnEngineTaskIntoTwoHalvesAndConservesOutcomeMinutes()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var topic=await f.Seed();
        var revision=await f.Db.StudyPlanRevisions.SingleAsync();var service=Student(f,revision.StudentId);
        var first=await f.Db.Set<StudyPlanTaskOutcome>().SingleAsync();var (second,estimate)=await SecondOutcome(f);
        var task=StudyPlanTask.Create(revision,new(2027,1,4),"Çarpanlar",90,topic.Id,true,"Kitap",30);task.SetWeeklyPlacement(topic.LessonId,"Matematik",1080);
        task.SetPlanDecision("Learn","Yeni konu: sırada bu kazanım var");
        var a=StudyPlanTaskOutcome.Create(task.Id,first.OutcomeId,first.EstimateId,50,"Catalog");a.CaptureText("M.8.1.1.1","Birinci");
        var b=StudyPlanTaskOutcome.Create(task.Id,second.Id,estimate.Id,40,"Catalog");b.CaptureText("M.8.1.1.2","İkinci");
        f.Db.AddRange(task,a,b);await f.Db.SaveChangesAsync();f.Db.ChangeTracker.Clear();

        var split=await service.SplitCalendarTaskAsync(revision.Id,task.Id,revision.Version);

        Assert.Equal(revision.Version+1,split.Version);
        var halves=split.Tasks.Where(x=>x.PlannedDate==new DateOnly(2027,1,4)).OrderBy(x=>x.PlannedStartMinute).ToArray();
        Assert.Equal(2,halves.Length);Assert.Equal(task.Id,halves[0].Id);
        Assert.Equal((1080,60),(halves[0].PlannedStartMinute!.Value,halves[0].PlannedMinutes));
        Assert.Equal((1140,30),(halves[1].PlannedStartMinute!.Value,halves[1].PlannedMinutes));
        Assert.All(halves,x=>{Assert.Equal("Çarpanlar",x.Title);Assert.Equal(topic.LessonId,x.WeeklyLessonId);Assert.Equal("Matematik",x.LessonName);Assert.Equal(topic.Id,x.TopicId);
            Assert.Equal("Learn",x.PlanKind);Assert.Equal("Yeni konu: sırada bu kazanım var",x.PlanReason);Assert.True(x.IsPinned);Assert.Equal("Kitap",x.SourceName);
            Assert.False(x.IsCompleted);Assert.Null(x.OriginalTaskId);Assert.Null(x.PredecessorTaskId);});
        Assert.Equal(30,halves.Sum(x=>x.PlannedQuestions));Assert.Equal(20,halves[0].PlannedQuestions);
        // The first half keeps the first outcome minutes; the link crossing the boundary is divided; estimates are kept.
        Assert.Equal(new[]{("M.8.1.1.1",50),("M.8.1.1.2",10)},halves[0].Outcomes!.Select(x=>(x.Code,x.AllocatedMinutes)).ToArray());
        Assert.Equal(new[]{("M.8.1.1.2",30)},halves[1].Outcomes!.Select(x=>(x.Code,x.AllocatedMinutes)).ToArray());
        var links=await f.Db.Set<StudyPlanTaskOutcome>().AsNoTracking().Where(x=>x.TaskId==task.Id||x.TaskId==halves[1].Id).ToArrayAsync();
        Assert.Equal(90,links.Sum(x=>x.AllocatedMinutes));
        Assert.All(links.Where(x=>x.OutcomeId==second.Id),x=>{Assert.Equal(estimate.Id,x.EstimateId);Assert.Equal("İkinci",x.NameSnapshot);Assert.Equal("Catalog",x.Origin);});
        // The seeded completed work and its link are untouched.
        Assert.Equal(30,(await f.Db.Set<StudyPlanTaskOutcome>().AsNoTracking().SingleAsync(x=>x.TaskId==first.TaskId)).AllocatedMinutes);

        // Each half can now be moved on its own.
        var moved=await service.MoveTasksAsync(revision.Id,new(split.Version,[halves[1].Id],new(2027,1,5),1080));
        Assert.Equal((new DateOnly(2027,1,5),1080),(moved.Tasks.Single(x=>x.Id==halves[1].Id).PlannedDate,moved.Tasks.Single(x=>x.Id==halves[1].Id).PlannedStartMinute!.Value));
        Assert.Equal(1080,moved.Tasks.Single(x=>x.Id==task.Id).PlannedStartMinute);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task CompletedHalvesStillCreditTheWholeOutcome()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var topic=await f.Seed();
        var revision=await f.Db.StudyPlanRevisions.SingleAsync();var service=Student(f,revision.StudentId);
        var seeded=await f.Db.Set<StudyPlanTaskOutcome>().SingleAsync();
        var task=StudyPlanTask.Create(revision,new(2026,10,20),"Çarpanlar",60,topic.Id);task.SetWeeklyPlacement(topic.LessonId,"Matematik",1080);
        var link=StudyPlanTaskOutcome.Create(task.Id,seeded.OutcomeId,seeded.EstimateId,60,"Catalog");
        f.Db.AddRange(task,link);await f.Db.SaveChangesAsync();f.Db.ChangeTracker.Clear();
        var split=await service.SplitCalendarTaskAsync(revision.Id,task.Id,revision.Version);
        var other=split.Tasks.Single(x=>x.PlannedDate==new DateOnly(2026,10,20)&&x.Id!=task.Id);
        Assert.Equal(new[]{30,30},split.Tasks.Where(x=>x.PlannedDate==new DateOnly(2026,10,20)).OrderBy(x=>x.PlannedStartMinute).Select(x=>x.Outcomes!.Single().AllocatedMinutes).ToArray());
        var done=await service.CompleteTaskAsync(revision.Id,task.Id,split.Version,30);
        await service.CompleteTaskAsync(revision.Id,other.Id,done.Version,30);
        var credits=await f.Db.StudyPlanTasks.AsNoTracking().Where(x=>x.IsCompleted&&x.PlannedDate==new DateOnly(2026,10,20))
            .Join(f.Db.Set<StudyPlanTaskOutcome>().AsNoTracking(),x=>x.Id,x=>x.TaskId,(t,l)=>new{Root=t.OriginalTaskId??t.Id,l.OutcomeId,l.AllocatedMinutes}).ToArrayAsync();
        // Completed-outcome credit counts one allocation per (task chain, outcome): both halves count, 60 minutes in total.
        Assert.Equal(2,credits.DistinctBy(x=>(x.Root,x.OutcomeId)).Count());Assert.Equal(60,credits.Sum(x=>x.AllocatedMinutes));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task SplitsAStudentsOwnTaskInADraftAndRoundsTheFirstHalfToHalfHours()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();
        var student=(await f.Db.StudyPlanRevisions.SingleAsync()).StudentId;var service=Student(f,student);
        var draft=await service.CreateDraftAsync(new("Taslak",[new(new(2027,1,8),"Soru çözümü",75,null,false,PlannedStartMinute:1080)]));
        var split=await service.SplitCalendarTaskAsync(draft.Id,draft.Tasks[0].Id,draft.Version);
        Assert.Equal(new[]{(1080,60),(1140,15)},split.Tasks.OrderBy(x=>x.PlannedStartMinute).Select(x=>(x.PlannedStartMinute!.Value,x.PlannedMinutes)).ToArray());
        Assert.All(split.Tasks,x=>{Assert.Equal("Soru çözümü",x.Title);Assert.Null(x.PlanKind);Assert.Null(x.WeeklyLessonId);Assert.Empty(x.Outcomes!);});
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task RefusesWorkThatCannotBeSplitAndAStaleVersion()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var topic=await f.Seed();
        var revision=await f.Db.StudyPlanRevisions.SingleAsync();var service=Student(f,revision.StudentId);
        StudyPlanTask Timed(string title,int minutes,int start){var t=StudyPlanTask.Create(revision,new(2027,1,4),title,minutes);t.SetWeeklyPlacement(topic.LessonId,"Matematik",start);return t;}
        var shortTask=Timed("Kısa",45,480);var untimed=StudyPlanTask.Create(revision,new(2027,1,4),"Saatsiz",60);
        var partial=Timed("Kısmi",60,600);var linked=Timed("Kararlı",60,720);var skipped=Timed("Atlanan",60,840);var ok=Timed("Bölünebilir",60,960);
        f.Db.AddRange(shortTask,untimed,partial,linked,skipped,ok);await f.Db.SaveChangesAsync();
        partial.RecordRealization(20,null,false,revision.StudentId,remainingMinutes:40,workedOn:new(2026,10,19));skipped.Skip();
        var decision=WeeklyDecision.Create(Guid.NewGuid(),revision.StudentId,Guid.NewGuid(),new(2027,1,4));decision.Edit("Takip","",new(2027,1,11),"Planned",DateTime.UtcNow);decision.LinkWork(linked.Id,null,DateTime.UtcNow);
        f.Db.Add(decision);await f.Db.SaveChangesAsync();f.Db.ChangeTracker.Clear();
        var completed=(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.IsCompleted)).Id;
        foreach(var id in new[]{shortTask.Id,untimed.Id,partial.Id,linked.Id,skipped.Id,completed})
            Assert.Equal("StudyPlanning.Conflict",(await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SplitCalendarTaskAsync(revision.Id,id,revision.Version))).Code);
        Assert.Equal("StudyPlanning.NotFound",(await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SplitCalendarTaskAsync(revision.Id,Guid.NewGuid(),revision.Version))).Code);
        Assert.Equal("StudyPlanning.Conflict",(await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SplitCalendarTaskAsync(revision.Id,ok.Id,revision.Version+1))).Code);
        Assert.Equal(7,await f.Db.StudyPlanTasks.CountAsync());Assert.Equal(revision.Version,(await f.Db.StudyPlanRevisions.AsNoTracking().SingleAsync()).Version);
        var split=await service.SplitCalendarTaskAsync(revision.Id,ok.Id,revision.Version);Assert.Equal(8,split.Tasks.Count);
        // The same request again carries the old version.
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SplitCalendarTaskAsync(revision.Id,ok.Id,revision.Version));
        // Another student cannot split it; an archived plan cannot change.
        f.User.UserId=Guid.NewGuid();await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SplitCalendarTaskAsync(revision.Id,ok.Id,split.Version));
        f.User.UserId=revision.StudentId;var tracked=await f.Db.StudyPlanRevisions.SingleAsync();tracked.Archive();await f.Db.SaveChangesAsync();f.Db.ChangeTracker.Clear();
        var archived=await f.Db.StudyPlanRevisions.AsNoTracking().SingleAsync();
        Assert.Equal("StudyPlanning.Conflict",(await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SplitCalendarTaskAsync(revision.Id,ok.Id,archived.Version))).Code);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TeacherSplitNotifiesTheStudentOnlyForAnActivePlan()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();var plan=await f.Db.StudyPlanRevisions.SingleAsync();var teacher=Guid.NewGuid();var student=plan.StudentId;
        f.User.UserId=teacher;f.User.Roles=["Teacher"];
        f.Db.AddRange(CoachingTeacherProfile.Create(teacher),CoachingStudentProfile.Create(student),CoachingTeacherStudentAssignment.Create(teacher,student,null,null,teacher));
        var task=StudyPlanTask.Create(plan,new(2027,1,5),"Matematik",60);task.Schedule(task.PlannedDate,1080);f.Db.Add(task);await f.Db.SaveChangesAsync();
        var policy=new CoachingAccessPolicy(f.User);var publisher=new Recorder();var directory=new OwnedCoachingDirectory(f.Db,new Accounts(teacher),f.User);
        var service=new CoachingManualStudyPlanService(f.Db,policy,new CoachingAutomaticStudyPlanPreviewService(f.Db,policy),publisher,new(policy,directory,new DenyAdmin()));
        var current=(await service.GetForStudentAsync(student,plan.Id))!;
        var split=await service.SplitCalendarForStudentAsync(student,plan.Id,task.Id,current.Version);
        Assert.Equal(2,split.Tasks.Count(x=>x.PlannedDate==new DateOnly(2027,1,5)));
        var notice=Assert.Single(publisher.Messages.OfType<StudyPlanUpdatedEvent>());Assert.Equal(student,notice.StudentId);
        Assert.Contains("Matematik",notice.ChangeDescription);Assert.Contains("18:00–18:30",notice.ChangeDescription);Assert.Contains("18:30–19:00",notice.ChangeDescription);
        var draft=await service.CreateForStudentAsync(student,new("Taslak",[new(new(2027,1,8),"Türkçe",60,null,false,PlannedStartMinute:1080)]));
        await service.SplitCalendarForStudentAsync(student,draft.Id,draft.Tasks[0].Id,draft.Version);Assert.Single(publisher.Messages);
        // The staff endpoint is a teacher write, like the other calendar edits.
        var method=typeof(CoachingStaffStudyController).GetMethod(nameof(CoachingStaffStudyController.SplitCalendarTask))!;
        Assert.Contains(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute),false).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(),x=>x.Roles=="Teacher");
        Assert.Single(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute),false));
        var route=Assert.Single(method.GetCustomAttributes(typeof(HttpPostAttribute),false).Cast<HttpPostAttribute>());Assert.Equal("plans/{id:guid}/tasks/{taskId:guid}/split",route.Template);
        var own=typeof(ManualStudyPlansController).GetMethod(nameof(ManualStudyPlansController.SplitCalendarTask))!;
        Assert.Equal("{id:guid}/tasks/{taskId:guid}/split",Assert.Single(own.GetCustomAttributes(typeof(HttpPostAttribute),false).Cast<HttpPostAttribute>()).Template);
    }
    private sealed class Recorder:ICoachingEventPublisher {public List<object> Messages {get;}=[];public Task PublishAsync<T>(T message,CancellationToken ct=default)where T:class {Messages.Add(message);return Task.CompletedTask;}}
    private sealed class DenyAdmin:ICoachingAdminScopeAuthorization {public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct)=>throw new NotSupportedException();}
    private sealed class Accounts(Guid teacher):ICoachingAccountDirectory
    {
        public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids,CancellationToken ct)=>Task.FromResult<IReadOnlyList<CoachingAccount>>(ids.Select(id=>new CoachingAccount(id,id==teacher?"Koç":"Öğrenci","Test","test@example.test",true,true,[id==teacher?"Teacher":"Student"],null)).ToArray());
        public Task<CoachingAccount?> FindByEmailAsync(string email,CancellationToken ct)=>throw new NotSupportedException();
        public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();
        public Task<bool> IsActiveInstitutionAsync(Guid id,CancellationToken ct)=>Task.FromResult(true);
        public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer,Guid target,CancellationToken ct)=>throw new NotSupportedException();
    }
}
