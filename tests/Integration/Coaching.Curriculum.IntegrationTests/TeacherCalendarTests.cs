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
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Coaching.Infrastructure.Data;
using System.Data.Common;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class TeacherCalendarTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task PublishingBetweenReportQueriesUsesTheNewPlansTimezoneWithoutLosingWork()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();var old=await f.Db.StudyPlanRevisions.SingleAsync();
        var teacher=Guid.NewGuid();f.User.UserId=teacher;f.User.Roles=["Teacher"];
        f.Db.AddRange(CoachingTeacherProfile.Create(teacher),CoachingStudentProfile.Create(old.StudentId),CoachingTeacherStudentAssignment.Create(teacher,old.StudentId,null,null,teacher));await f.Db.SaveChangesAsync();
        var hook=new PublicationHook(async()=>{
            await using var writer=new CoachingDbContext(pg.Options);var previous=await writer.StudyPlanRevisions.SingleAsync(x=>x.Id==old.Id);previous.Archive();await writer.SaveChangesAsync();
            var next=StudyPlanRevision.Create(old.StudentId,Guid.NewGuid(),1,"Yeni plan");next.SetWeeklySource(new(0,null,new(2027,1,1),new(2027,1,10),false,"test",null,null,"Europe/London"));next.Activate();
            var task=StudyPlanTask.Create(next,new(2027,1,5),"Londra saatinde gelecek çalışma",30);task.Schedule(task.PlannedDate,840);writer.AddRange(next,task);await writer.SaveChangesAsync();
        });
        await using var db=new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>(pg.Options).AddInterceptors(hook).Options);
        var api=new CoachingTeacherStudyController(db,new OwnedCoachingDirectory(db,new Accounts(teacher,old.StudentId),f.User),f.User,new FixedClock());
        var response=Assert.IsType<OkObjectResult>(await api.Overview(new(2027,1,4),new(2027,1,6),1,25,null,default));
        var row=Assert.Single(Assert.IsType<TeacherStudyOverview>(response.Value).Items);Assert.Equal(1,row.PendingTasks);Assert.Equal(0,row.OverdueTasks);Assert.Equal("Londra saatinde gelecek çalışma",Assert.Single(row.Tasks!).Title);
    }
    private sealed class PublicationHook(Func<Task> publish):DbCommandInterceptor
    {
        private bool fired;
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData data,DbDataReader result,CancellationToken ct=default)
        {
            if(!fired&&command.CommandText.Contains("\"WeeklySource\"")&&!command.CommandText.Contains("JOIN")){fired=true;await publish();}
            return result;
        }
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TeacherSeesScopedCompletionAndOverdueTotalsAndCreatesOrCorrectsTheStudentsPlan()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();
        var original=await f.Db.StudyPlanRevisions.SingleAsync();var student=original.StudentId;var teacher=Guid.NewGuid();
        var accounts=new Accounts(teacher,student);f.User.UserId=teacher;f.User.Roles=["Teacher"];
        f.Db.AddRange(CoachingTeacherProfile.Create(teacher),CoachingStudentProfile.Create(student),CoachingTeacherStudentAssignment.Create(teacher,student,null,null,teacher));
        var past=StudyPlanTask.Create(original,new(2027,1,4),"Geciken",30);
        var today=StudyPlanTask.Create(original,new(2027,1,5),"Bugün saati geçen",30);today.Schedule(today.PlannedDate,540);
        var future=StudyPlanTask.Create(original,new(2027,1,5),"Akşam",30);future.Schedule(future.PlannedDate,1080);
        var done=StudyPlanTask.Create(original,new(2027,1,4),"Tamamlanan",30);done.Complete(12,0);
        f.Db.AddRange(past,today,future,done);await f.Db.SaveChangesAsync();f.Db.ChangeTracker.Clear();
        var directory=new OwnedCoachingDirectory(f.Db,accounts,f.User);var scope=new CoachingStudyPlanAccess(new CoachingAccessPolicy(f.User),directory,new DenyAdmin());
        var plans=new CoachingManualStudyPlanService(f.Db,new CoachingAccessPolicy(f.User),new CoachingAutomaticStudyPlanPreviewService(f.Db,new CoachingAccessPolicy(f.User)),new CurriculumManagementTests.NoopPublisher(),scope);
        var api=new CoachingTeacherStudyController(f.Db,directory,f.User,new FixedClock());
        var result=Assert.IsType<OkObjectResult>(await api.Overview(new(2027,1,4),new(2027,1,6),1,25,null,default));
        var data=Assert.IsType<TeacherStudyOverview>(result.Value);var row=Assert.Single(data.Items);
        Assert.Equal(student,row.StudentId);Assert.Equal(1,row.CompletedTasks);Assert.Equal(3,row.PendingTasks);Assert.Equal(2,row.OverdueTasks);Assert.Equal(12,row.ActualMinutes);
        Assert.Equal(4,row.Tasks!.Count);Assert.Equal(2,row.Tasks.Count(t=>t.IsOverdue));Assert.Contains(row.Tasks,t=>t.TaskId==done.Id&&t.ActualMinutes==12);
        Assert.IsType<BadRequestObjectResult>(await api.Overview(new(2027,2,4),new(2027,1,6),1,25,null,default));
        Assert.IsType<BadRequestObjectResult>(await api.Overview(new(2027,1,4),new(2027,1,6),0,25,null,default));
        Assert.IsType<OkObjectResult>(await new CoachingTeacherStudyController(f.Db,directory,f.User).Overview(new(2030,1,1),new(2030,1,7),1,25,null,default));
        f.User.Roles=["Student"];Assert.IsType<ForbidResult>(await api.Overview(new(2027,1,4),new(2027,1,6),1,25,null,default));
        f.User.UserId=null;Assert.IsType<ForbidResult>(await api.Overview(new(2027,1,4),new(2027,1,6),1,25,null,default));
        f.User.UserId=teacher;f.User.Roles=["Teacher"];accounts.Enabled=false;
        Assert.Equal(403,Assert.IsType<ObjectResult>(await api.Overview(new(2027,1,4),new(2027,1,6),1,25,null,default)).StatusCode);accounts.Enabled=true;
        var current=(await plans.GetForStudentAsync(student,original.Id))!;
        var corrected=await plans.CorrectTitleForStudentAsync(student,original.Id,past.Id,current.Version,"Düzeltilen başlık");
        Assert.Equal("Düzeltilen başlık",corrected.Tasks.Single(t=>t.Id==past.Id).Title);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>plans.CorrectTitleForStudentAsync(student,original.Id,done.Id,corrected.Version,"Geçmişi değiştir"));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>plans.MoveForStudentAsync(Guid.NewGuid(),original.Id,new(corrected.Version,[future.Id],new(2027,1,6),600)));
        var moved=await plans.MoveForStudentAsync(student,original.Id,new(corrected.Version,[future.Id],new(2027,1,6),600));
        Assert.Equal(600,moved.Tasks.Single(t=>t.Id==future.Id).PlannedStartMinute);
        var draft=await plans.CreateForStudentAsync(student,new("Öğretmen planı",[new(new(2027,1,7),"Matematik",30,null,false,PlannedStartMinute:1080)]));
        var published=await plans.PublishForStudentAsync(student,draft.Id,draft.Version);
        f.User.UserId=student;f.User.Roles=["Student"];
        Assert.Equal(1080,(await plans.GetAsync(published.Id))!.Tasks.Single().PlannedStartMinute);
        Assert.Equal(12,(await plans.GetAsync(original.Id))!.Tasks.Single(t=>t.Id==done.Id).ActualMinutes);
        f.User.UserId=teacher;f.User.Roles=["Teacher"];
        var assignment=await f.Db.CoachingTeacherStudentAssignments.SingleAsync();assignment.SetActive(false,teacher);await f.Db.SaveChangesAsync();
        result=Assert.IsType<OkObjectResult>(await api.Overview(new(2027,1,4),new(2027,1,6),1,25,null,default));
        Assert.Empty(Assert.IsType<TeacherStudyOverview>(result.Value).Items);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>plans.GetForStudentAsync(student,published.Id));
    }
    private sealed class FixedClock:TimeProvider { public override DateTimeOffset GetUtcNow()=>new(2027,1,5,12,0,0,TimeSpan.Zero); }
    private sealed class DenyAdmin:ICoachingAdminScopeAuthorization { public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct)=>throw new NotSupportedException(); }
    private sealed class Accounts(Guid teacher,Guid student):ICoachingAccountDirectory
    {
        public bool Enabled {get;set;}=true;
        public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids,CancellationToken ct)=>Task.FromResult<IReadOnlyList<CoachingAccount>>(
            ids.Select(id=>new CoachingAccount(id,id==teacher?"Koç":id==student?"Öğrenci":"Yabancı","Test","test@example.test",Enabled,true,[id==teacher?"Teacher":"Student"],null)).ToArray());
        public Task<CoachingAccount?> FindByEmailAsync(string email,CancellationToken ct)=>throw new NotSupportedException();
        public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();
        public Task<bool> IsActiveInstitutionAsync(Guid id,CancellationToken ct)=>Task.FromResult(true);
        public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer,Guid target,CancellationToken ct)=>throw new NotSupportedException();
    }
}
