using System.Text.Json;
using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class DecisionResponsibilityAndPostponementTests
{
    private static readonly DateOnly Week=new(2026,10,5);
    private static readonly DateTimeOffset Now=new(2026,10,20,12,0,0,TimeSpan.Zero);

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task InstitutionTeacherReplacementTransfersOnlyOpenSameScopeDecisionsAndKeepsCreatorAndLinkedWork()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var s=await Seed(pg.Db);
        var reviews=Reviews(pg.Db,s.User,s.Accounts);var decision=await reviews.SaveDecisionAsync(s.Student,Week,Guid.NewGuid(),new(null,"Eski karar","Korunan not",Week.AddDays(10),"Planned"),default);
        var plan=StudyPlanRevision.Create(s.Student,Guid.NewGuid(),1,"Korunan plan");plan.Activate();
        var task=StudyPlanTask.Create(plan,Week.AddDays(10),"Korunan çalışma",30);
        var assignment=Assignment.Create(s.Old,"Eski öğretmenin ödevi",Now.UtcDateTime.AddDays(3),institutionId:s.Institution);assignment.AssignToStudents([s.Student]);
        var closed=WeeklyDecision.Create(Guid.NewGuid(),s.Student,s.Old,Week,s.Institution);closed.Edit("Kapalı","",Week.AddDays(10),"Completed",Now.UtcDateTime);
        var otherScope=WeeklyDecision.Create(Guid.NewGuid(),s.Student,s.Old,Week,Guid.NewGuid());otherScope.Edit("Başka kurum","",Week.AddDays(10),"Planned",Now.UtcDateTime);
        pg.Db.AddRange(plan,task,assignment,closed,otherScope);await pg.Db.SaveChangesAsync();
        decision=await reviews.SaveLinksAsync(s.Student,Week,decision.Id,new(decision.Version,task.Id,assignment.Id),default);
        var history=decision.History.ToArray();var workBefore=JsonSerializer.Serialize(await pg.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync());var assignmentBefore=JsonSerializer.Serialize(await pg.Db.AssignmentStudents.AsNoTracking().ToArrayAsync());
        var store=new CoachingProfileStore(pg.Db,s.Accounts,clock:new Clock());
        // Roadmap Y4: choosing a teacher adds one; the replacement is complete when the old teacher's link ends.
        await store.UpdateInstitutionStudentAsync(s.Admin,s.Institution,s.Student,8,s.Next,default);
        await store.EndInstitutionTeacherLinkAsync(s.Admin,s.Institution,s.Student,s.Old,default);pg.Db.ChangeTracker.Clear();
        var row=await pg.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync(x=>x.Id==decision.Id);
        Assert.Equal(s.Old,row.TeacherId);Assert.Equal(s.Next,row.ResponsibleTeacherId);Assert.Equal(Week.AddDays(10),row.InitialDueOn);Assert.Equal(Week,row.ReviewWeekStart);
        Assert.Equal(task.Id,row.StudyTaskId);Assert.Equal(assignment.Id,row.AssignmentId);Assert.Equal(history,row.History.Take(history.Length));
        var change=row.History.Last();Assert.Equal("ResponsibilityTransfer",change.Action);Assert.Equal(s.Old,change.PreviousTeacherId);Assert.Equal(s.Next,change.ResponsibleTeacherId);Assert.Equal(s.Admin,change.ActorUserId);
        Assert.Equal(s.Old,(await pg.Db.Set<WeeklyDecision>().SingleAsync(x=>x.Id==closed.Id)).ResponsibleTeacherId);
        Assert.Equal(s.Old,(await pg.Db.Set<WeeklyDecision>().SingleAsync(x=>x.Id==otherScope.Id)).ResponsibleTeacherId);
        Assert.Equal(workBefore,JsonSerializer.Serialize(await pg.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync()));Assert.Equal(assignmentBefore,JsonSerializer.Serialize(await pg.Db.AssignmentStudents.AsNoTracking().ToArrayAsync()));
        await store.UpdateInstitutionStudentAsync(s.Admin,s.Institution,s.Student,8,s.Next,default);
        Assert.Equal(row.History.Count,(await pg.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync(x=>x.Id==row.Id)).History.Count);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>reviews.SaveDecisionAsync(s.Student,Week,row.Id,new(row.Version,row.Title,row.Note,row.DueOn,"Completed"),default));
        s.User.UserId=s.Next;
        var own=Assert.Single((await reviews.GetAsync(s.Student,new(2026,10,19),1,default)).Decisions.Where(x=>x.Id==row.Id));
        Assert.True(own.CanEdit);Assert.Equal("Eski öğretmenin ödevi",own.Work!.Assignment!.Title);
        var tracking=new CoachingTeacherTrackingController(pg.Db,new CoachingAccessPolicy(s.User),new OwnedCoachingDirectory(pg.Db,s.Accounts,s.User),s.Accounts,new Clock());
        var page=Assert.IsType<TeacherTrackingPage>(Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(await tracking.Get(Week,new(2026,10,25),"decision")).Value);
        Assert.Contains(page.Items,x=>x.RecordId==row.Id);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task PostponementRequiresReasonOwnerVersionAndFutureDateAndPreservesInitialDeadlineAndWork()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var s=await Seed(pg.Db);var reviews=Reviews(pg.Db,s.User,s.Accounts);
        var row=await reviews.SaveDecisionAsync(s.Student,Week,Guid.NewGuid(),new(null,"Takip","İlk not",Week.AddDays(10),"Planned"),default);
        var original=await pg.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync();var oldHistory=original.History.ToArray();
        foreach(var reason in new[]{"","   ","kısa",new string('a',1001)})
            await Assert.ThrowsAsync<ArgumentException>(()=>reviews.PostponeAsync(s.Student,Week,row.Id,new(row.Version,new(2026,10,28),reason),default));
        await Assert.ThrowsAsync<ArgumentException>(()=>reviews.PostponeAsync(s.Student,Week,row.Id,new(row.Version,new(2026,10,20),"Geçerli gerekçe"),default));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>reviews.PostponeAsync(s.Student,Week,row.Id,new(row.Version+10,new(2026,10,28),"Geçerli gerekçe"),default));
        s.User.UserId=s.Next;await Assert.ThrowsAsync<BusinessRuleException>(()=>reviews.PostponeAsync(s.Student,Week,row.Id,new(row.Version,new(2026,10,28),"Geçerli gerekçe"),default));s.User.UserId=s.Old;
        var request=new WeeklyDecisionPostpone(row.Version,new(2026,10,28),"  Hastalık nedeniyle ek süre  ");
        var saved=await reviews.PostponeAsync(s.Student,Week,row.Id,request,default);
        Assert.Equal(new DateOnly(2026,10,28),saved.DueOn);Assert.Equal(original.DueOn,saved.InitialDueOn);Assert.Equal(Week,saved.ReviewWeekStart);Assert.False(saved.IsOverdue);
        Assert.Equal(oldHistory,saved.History.Take(oldHistory.Length));var last=saved.History.Last();Assert.Equal("Postponement",last.Action);Assert.Equal(original.DueOn,last.PreviousDueOn);Assert.Equal("Hastalık nedeniyle ek süre",last.Reason);Assert.Equal(s.Old,last.ActorUserId);
        Assert.Equal(saved.Version,(await reviews.PostponeAsync(s.Student,Week,row.Id,request,default)).Version);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>reviews.PostponeAsync(s.Student,Week,row.Id,request with{Reason="Farklı gerekçe"},default));
        await Assert.ThrowsAsync<ArgumentException>(()=>reviews.SaveDecisionAsync(s.Student,Week,row.Id,new(saved.Version,saved.Title,saved.Note,new(2026,11,1),"Planned"),default));
        var closed=await reviews.SaveDecisionAsync(s.Student,Week,row.Id,new(saved.Version,saved.Title,saved.Note,saved.DueOn,"Completed"),default);
        await Assert.ThrowsAsync<ArgumentException>(()=>reviews.PostponeAsync(s.Student,Week,row.Id,new(closed.Version,new(2026,11,5),"Geçerli gerekçe"),default));
        Assert.Equal(1,await pg.Db.Set<WeeklyDecision>().CountAsync());
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>pg.Db.GetService<IMigrator>().MigrateAsync("20261024202303_PreservePublishedPlanHistory"));
        // EF Core 10 commits each rolled-back migration on its own, so a refused downgrade can leave newer columns removed;
        // the kept data is therefore read with SQL that does not depend on the current model.
        Assert.Equal("2026-10-28",await pg.Db.Database.SqlQueryRaw<string>("""SELECT "DueOn"::text AS "Value" FROM coaching.weekly_decisions""").SingleAsync());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task AdditionalTeacherDoesNotStealDecisionsAndNullAssignmentWaitsForExplicitReplacement()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var s=await Seed(pg.Db);var reviews=Reviews(pg.Db,s.User,s.Accounts);var store=new CoachingProfileStore(pg.Db,s.Accounts,clock:new Clock());
        var row=await reviews.SaveDecisionAsync(s.Student,Week,Guid.NewGuid(),new(null,"Açık","",Week.AddDays(10),"Planned"),default);
        await store.SetAssignmentAsync(s.Admin,s.Next,s.Student,s.Institution,null,true,default);
        Assert.Equal(s.Old,(await pg.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).ResponsibleTeacherId);
        await store.UpdateInstitutionStudentAsync(s.Admin,s.Institution,s.Student,8,null,default);
        Assert.Equal(s.Old,(await pg.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).ResponsibleTeacherId);
        await store.UpdateInstitutionStudentAsync(s.Admin,s.Institution,s.Student,8,s.Next,default);
        Assert.Equal(s.Old,(await pg.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).ResponsibleTeacherId);
        // Ending the first teacher's link hands its open decisions to the teacher who stays.
        await store.EndInstitutionTeacherLinkAsync(s.Admin,s.Institution,s.Student,s.Old,default);
        Assert.Equal(s.Next,(await pg.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).ResponsibleTeacherId);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task IndependentInvitationAfterDisconnectionTransfersOpenDecisionAndOldTeacherCannotPostponeIt()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var s=await Seed(pg.Db,false);var reviews=Reviews(pg.Db,s.User,s.Accounts);
        var row=await reviews.SaveDecisionAsync(s.Student,Week,Guid.NewGuid(),new(null,"Bağımsız takip","",Week.AddDays(10),"Planned"),default);
        var store=new CoachingProfileStore(pg.Db,s.Accounts,clock:new Clock());await store.EndAssignmentAsync(s.Old,s.Old,s.Student,false,default);
        var invites=new CoachingInvitationStore(pg.Db,s.Accounts,store,new Clock(),new CurriculumManagementTests.NoopPublisher(),new ConfigurationBuilder().Build());
        var invitation=await invites.SendTeacherAsync(s.Next,"test@example.test",null,default);await invites.AcceptAsync(s.Student,invitation.Id,default);
        var updated=await pg.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync();Assert.Equal(s.Next,updated.ResponsibleTeacherId);Assert.Equal(s.Old,updated.TeacherId);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>reviews.PostponeAsync(s.Student,Week,row.Id,new(updated.Version,new(2026,10,28),"Geçerli gerekçe"),default));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigrationPreservesLegacyHistoryVersionsAndCreatorAndInitializesOnlyProvableScope()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create("20261024202303_PreservePublishedPlanHistory");var s=await Seed(pg.Db);
        var id=Guid.NewGuid();var unknown=Guid.NewGuid();var unrelated=Guid.NewGuid();
        var history=JsonSerializer.Serialize(new[]{new {At=Now.UtcDateTime,Title="Eski başlık",Note="İlk not",DueOn=Week.AddDays(9),Status="Planned"}});
        foreach(var pair in new[]{(id,s.Old),(unknown,unrelated)})
            await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO coaching.weekly_decisions ("Id","StudentId","TeacherId","ReviewWeekStart","Title","Note","DueOn","Status","HistoryJson","CreatedAt","Version")
                VALUES ({pair.Item1},{s.Student},{pair.Item2},{Week},'Eski karar','Korunan not',{Week.AddDays(10)},'Planned',{history}::jsonb,{Now.UtcDateTime},17)
                """);
        async Task<string[]> Rows()=>await pg.Db.Database.SqlQueryRaw<string>("""SELECT (to_jsonb(d)-'ResponsibleTeacherId'-'InitialDueOn'-'InstitutionId'-'HasAssignmentScope'-'SourceReflectionId'-'SourceReflectionVersion'-'SourceReflectionSnapshotJson'-'PreviousDecisionId'-'PreviousDecisionSnapshotJson')::text AS "Value" FROM coaching.weekly_decisions d ORDER BY "Id" """).ToArrayAsync();
        var before=await Rows();await pg.Db.Database.MigrateAsync();await pg.Db.Database.MigrateAsync();
        Assert.Equal(before,await Rows());var row=await pg.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync(x=>x.Id==id);
        Assert.Equal(17,row.Version);Assert.Equal(s.Old,row.TeacherId);Assert.Equal(s.Old,row.ResponsibleTeacherId);Assert.Equal(Week.AddDays(9),row.InitialDueOn);Assert.True(row.HasAssignmentScope);Assert.Equal(s.Institution,row.InstitutionId);
        Assert.False((await pg.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync(x=>x.Id==unknown)).HasAssignmentScope);
        Assert.False(pg.Db.Database.HasPendingModelChanges());
        await pg.Db.GetService<IMigrator>().MigrateAsync("20261024202303_PreservePublishedPlanHistory");Assert.Equal(before,await Rows());await pg.Db.Database.MigrateAsync();Assert.Equal(before,await Rows());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task UnexpiredOriginalWeekDeadlineRequiresReasonAndKeepsItsInitialDate()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var s=await Seed(pg.Db);var reviews=Reviews(pg.Db,s.User,s.Accounts);var week=new DateOnly(2026,10,12);
        var row=await reviews.SaveDecisionAsync(s.Student,week,Guid.NewGuid(),new(null,"Gelecek karar","",new(2026,10,22),"Planned"),default);
        await Assert.ThrowsAsync<ArgumentException>(()=>reviews.SaveDecisionAsync(s.Student,week,row.Id,new(row.Version,row.Title,row.Note,new(2026,10,24),row.Status),default));
        var changed=await reviews.ChangeDeadlineAsync(s.Student,week,row.Id,new(row.Version,new(2026,10,24),"Çalışma takvimi değişti"),default);
        Assert.Equal(new DateOnly(2026,10,24),changed.DueOn);Assert.Equal(new DateOnly(2026,10,22),changed.InitialDueOn);
        await Assert.ThrowsAsync<ArgumentException>(()=>reviews.SaveDecisionAsync(s.Student,week,row.Id,new(changed.Version,row.Title,row.Note,new(2026,10,28),row.Status),default));
    }

    internal sealed record Scenario(Guid Student,Guid Old,Guid Next,Guid Admin,Guid Institution,CurriculumManagementTests.Actor User,Accounts Accounts);
    internal static async Task<Scenario> Seed(CoachingDbContext db,bool institution=true)
    {
        var student=Guid.NewGuid();var old=Guid.NewGuid();var next=Guid.NewGuid();var admin=Guid.NewGuid();var inst=Guid.NewGuid();
        db.AddRange(CoachingStudentProfile.Create(student),CoachingTeacherProfile.Create(old),CoachingTeacherProfile.Create(next),CoachingTeacherStudentAssignment.Create(old,student,institution?inst:null,null,admin));
        if(institution)db.AddRange(CoachingInstitutionMembership.Create(inst,student,CoachingMemberRole.Student,admin),CoachingInstitutionMembership.Create(inst,old,CoachingMemberRole.Teacher,admin),CoachingInstitutionMembership.Create(inst,next,CoachingMemberRole.Teacher,admin));
        await db.SaveChangesAsync();db.ChangeTracker.Clear();return new(student,old,next,admin,inst,new(){UserId=old,Roles=["Teacher"]},new());
    }
    internal static CoachingWeeklyReviewService Reviews(CoachingDbContext db,CurriculumManagementTests.Actor user,Accounts accounts,ICoachingEventPublisher? events=null)
    {var access=new CoachingAccessPolicy(user);var directory=new OwnedCoachingDirectory(db,accounts,user);return new(db,access,new(access,directory,new CurriculumManagementTests.Scope()),directory,new Clock(),events??new CurriculumManagementTests.NoopPublisher());}
    internal sealed class Clock:TimeProvider {public override DateTimeOffset GetUtcNow()=>Now;}
    internal sealed class Accounts:ICoachingAccountDirectory
    {
        public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids,CancellationToken ct)=>Task.FromResult<IReadOnlyList<CoachingAccount>>(ids.Select(id=>new CoachingAccount(id,"Test","User","test@example.test",true,true,["Teacher","Student"],null)).ToArray());
        public Task<CoachingAccount?> FindByEmailAsync(string email,CancellationToken ct)=>Task.FromResult<CoachingAccount?>(null);
        public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id,CancellationToken ct)=>Task.FromResult<CoachingAdminAccessScope?>(new(true,null));
        public Task<bool> IsActiveInstitutionAsync(Guid id,CancellationToken ct)=>Task.FromResult(true);
        public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer,Guid target,CancellationToken ct)=>Task.FromResult<CoachingAgreementRepresentativeAuthorization?>(null);
    }
}
