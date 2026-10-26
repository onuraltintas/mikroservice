using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Contracts.Events.Coaching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class WeeklyFollowUpPostgresTests
{
    private static readonly DateOnly Week = new(2026, 10, 12);
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ReflectionAndDecisionLeaveTrackingAfterSaveAndCompletionAndHomeOnlyShowsOpenDecisions()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var student = Guid.NewGuid(); var teacher = Guid.NewGuid(); var messages = new Publisher();
        var service = await WeeklyReviewPostgresTests.Setup(pg, student, teacher, messages); var user = pg.Data.User;
        foreach (var row in pg.Db.CoachingTeacherStudentAssignments) pg.Db.Entry(row).Property(x => x.StartDate).CurrentValue = new DateTime(2026,10,13,0,0,0,DateTimeKind.Utc);
        foreach (var row in pg.Db.CoachingStudentProfiles) pg.Db.Entry(row).Property(x => x.CreatedAt).CurrentValue = new DateTime(2026,10,13,0,0,0,DateTimeKind.Utc);
        await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();
        var accounts = new Accounts(teacher); var policy = new CoachingAccessPolicy(user);
        var api = new CoachingTeacherTrackingController(pg.Db, policy, new OwnedCoachingDirectory(pg.Db, accounts, user), accounts, new Clock());
        async Task<TeacherTrackingPage> Page(string mode = "all", int page = 1) => Assert.IsType<TeacherTrackingPage>(Assert.IsType<OkObjectResult>(await api.Get(Week, Week.AddDays(8), mode, pageNumber:page, pageSize:1)).Value);
        user.UserId = teacher; user.Roles = ["Teacher"];
        var waiting = await Page("reflection"); Assert.Equal(1, waiting.AwaitingReflectionCount); Assert.Equal(Week, Assert.Single(waiting.Items).WeekStart);
        user.UserId = student; user.Roles = ["Student"]; Assert.True((await service.HomeAsync(default)).NeedsReflection);
        var saved = await service.SaveReflectionAsync(Week, new(null,"Hafta iyi geçti","","Tekrar",4),default);
        Assert.Single(messages.Messages); Assert.Equal(teacher, Assert.Single(Assert.IsType<WeeklyReflectionSavedEvent>(messages.Messages[0]).TeacherIds));
        await service.SaveReflectionAsync(Week, new(saved.Version,"Hafta iyi geçti","","Tekrar",4),default); Assert.Single(messages.Messages);
        user.UserId = teacher; user.Roles = ["Teacher"]; Assert.Empty((await Page("reflection")).Items);
        var input = new WeeklyDecisionSave(null,"Tekrar kararı","",new(2026,10,19),"Planned"); var id = Guid.NewGuid();
        var decision = await service.SaveDecisionAsync(student,Week,id,input,default);
        Assert.True(Assert.IsType<WeeklyDecisionChangedEvent>(messages.Messages[1]).Created);
        Assert.Equal(1,(await Page("decision")).OverdueDecisionCount); Assert.Equal(id,Assert.Single((await Page("decision")).Items).RecordId);
        user.UserId = student; user.Roles = ["Student"]; var home = await service.HomeAsync(default);
        Assert.False(home.NeedsReflection); Assert.Equal(1,home.OpenDecisions); Assert.Equal(1,home.OverdueDecisions); Assert.Equal(id,Assert.Single(home.Decisions).Id);
        user.UserId = teacher; user.Roles = ["Teacher"];
        await service.SaveDecisionAsync(student,Week,id,input with { ExpectedVersion = decision.Version, Status = "Completed" },default);
        Assert.False(Assert.IsType<WeeklyDecisionChangedEvent>(messages.Messages[2]).Created); Assert.Empty((await Page("decision")).Items);
        user.UserId = student; user.Roles = ["Student"]; Assert.Empty((await service.HomeAsync(default)).Decisions);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task DeepLinkFindsDecisionBeyondFirstPageAndConsentBlocksTeacherNotifications()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var student=Guid.NewGuid();var teacher=Guid.NewGuid();var messages=new Publisher();
        var service=await WeeklyReviewPostgresTests.Setup(pg,student,teacher,messages);var user=pg.Data.User;
        user.UserId=teacher;user.Roles=["Teacher"];
        for(var i=0;i<24;i++) await service.SaveDecisionAsync(student,Week,Guid.NewGuid(),new(null,"Karar "+i,"",Week.AddDays(7),"Planned"),default);
        var last=(await service.GetAsync(student,Week,2,default)).Decisions.Last();
        var focused=await service.GetAsync(student,Week,1,default,last.Id);Assert.Equal(2,focused.DecisionPage);Assert.Contains(focused.Decisions,x=>x.Id==last.Id);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.GetAsync(student,Week,1,default,Guid.NewGuid()));
        user.UserId=student;user.Roles=["Student"];var home=await service.HomeAsync(default);Assert.Equal(24,home.OpenDecisions);Assert.Equal(5,home.Decisions.Count);
        (await pg.Db.CoachingStudentProfiles.SingleAsync()).UpdatePersonal(null,null,null,false);await pg.Db.SaveChangesAsync();
        await service.SaveReflectionAsync(Week,new(null,"Kendi haftam","","Odak",3),default);
        Assert.DoesNotContain(messages.Messages,x=>x is WeeklyReflectionSavedEvent);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task NewAssignmentsAreNotFalselyMissingAndEarlierUnsubmittedWeeksStayInBacklogAndSharingIsRespected()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();
        await WeeklyReviewPostgresTests.Setup(pg,student,teacher);var user=pg.Data.User;user.UserId=teacher;user.Roles=["Teacher"];
        var accounts=new Accounts(teacher);var api=new CoachingTeacherTrackingController(pg.Db,new CoachingAccessPolicy(user),new OwnedCoachingDirectory(pg.Db,accounts,user),accounts,new Clock());
        async Task<TeacherTrackingPage> Page(DateOnly from,DateOnly to)=>Assert.IsType<TeacherTrackingPage>(Assert.IsType<OkObjectResult>(await api.Get(from,to,"reflection")).Value);
        Assert.Empty((await Page(Week,Week.AddDays(8))).Items);
        var assignment=await pg.Db.CoachingTeacherStudentAssignments.SingleAsync();pg.Db.Entry(assignment).Property(x=>x.StartDate).CurrentValue=new DateTime(2026,10,13,0,0,0,DateTimeKind.Utc);await pg.Db.SaveChangesAsync();
        Assert.Single((await Page(Week,Week.AddDays(8))).Items);Assert.Single((await Page(new(2026,10,19),new(2026,10,20))).Items);
        (await pg.Db.CoachingStudentProfiles.SingleAsync()).UpdatePersonal(null,null,null,false);await pg.Db.SaveChangesAsync();Assert.Empty((await Page(Week,Week.AddDays(8))).Items);
    }
    internal sealed class Publisher:ICoachingEventPublisher { public List<object> Messages=[];public Task PublishAsync<T>(T message,CancellationToken ct=default)where T:class{Messages.Add(message);return Task.CompletedTask;} }
    internal sealed class Clock:TimeProvider {public override DateTimeOffset GetUtcNow()=>new(2026,10,20,12,0,0,TimeSpan.Zero);}
    internal sealed class Accounts(Guid teacher):ICoachingAccountDirectory {
        public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids,CancellationToken ct)=>Task.FromResult<IReadOnlyList<CoachingAccount>>(ids.Select(id=>new CoachingAccount(id,"Test","User","test@example.test",true,true,[id==teacher?"Teacher":"Student"],null)).ToArray());
        public Task<CoachingAccount?> FindByEmailAsync(string email,CancellationToken ct)=>throw new NotSupportedException();public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();public Task<bool> IsActiveInstitutionAsync(Guid id,CancellationToken ct)=>Task.FromResult(true);public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer,Guid target,CancellationToken ct)=>throw new NotSupportedException();
    }
}
