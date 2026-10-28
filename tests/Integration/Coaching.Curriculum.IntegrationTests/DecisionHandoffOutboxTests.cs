using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Messaging;
using Coaching.Infrastructure.Profiles;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class DecisionHandoffOutboxTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task HandoffAndPostponementRollbackTogetherWithNotificationsAndRetryWithoutDuplicateHistory()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var s=await DecisionResponsibilityAndPostponementTests.Seed(pg.Db);
        var week=new DateOnly(2026,10,5);var original=await DecisionResponsibilityAndPostponementTests.Reviews(pg.Db,s.User,s.Accounts).SaveDecisionAsync(s.Student,week,Guid.NewGuid(),new(null,"Korunan karar","",week.AddDays(10),"Planned"),default);
        var failure=new Failure();var services=new ServiceCollection();services.AddLogging();
        services.AddDbContext<CoachingDbContext>(o=>o.UseNpgsql(pg.Db.Database.GetConnectionString()).AddInterceptors(failure));
        services.AddMassTransit(x=>{x.AddEntityFrameworkOutbox<CoachingDbContext>(o=>{o.UsePostgres();o.UseBusOutbox();});x.UsingInMemory((ctx,cfg)=>cfg.ConfigureEndpoints(ctx));});
        await using var provider=services.BuildServiceProvider();await using var scope=provider.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<CoachingDbContext>();var publisher=new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>());
        var store=new CoachingProfileStore(db,s.Accounts,publisher,new DecisionResponsibilityAndPostponementTests.Clock());
        // Roadmap Y4: the new teacher is added first; the handoff happens when the old teacher's link ends.
        await store.UpdateInstitutionStudentAsync(s.Admin,s.Institution,s.Student,9,s.Next,default);Assert.Empty(await db.Set<OutboxMessage>().AsNoTracking().ToArrayAsync());
        failure.Enabled=true;await Assert.ThrowsAsync<InvalidOperationException>(()=>store.EndInstitutionTeacherLinkAsync(s.Admin,s.Institution,s.Student,s.Old,default));
        Assert.Equal(s.Old,(await db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).ResponsibleTeacherId);
        Assert.True(await db.CoachingTeacherStudentAssignments.AsNoTracking().AnyAsync(x=>x.TeacherUserId==s.Old&&x.IsActive));Assert.Empty(await db.Set<OutboxMessage>().AsNoTracking().ToArrayAsync());
        failure.Enabled=false;await store.EndInstitutionTeacherLinkAsync(s.Admin,s.Institution,s.Student,s.Old,default);
        var transferred=await db.Set<WeeklyDecision>().AsNoTracking().SingleAsync();Assert.Equal(s.Next,transferred.ResponsibleTeacherId);Assert.Equal(original.History.Count+1,transferred.History.Count);
        var message=Assert.Single(await db.Set<OutboxMessage>().AsNoTracking().ToArrayAsync());Assert.Contains("responsibilityTransferred",message.Body);Assert.DoesNotContain("Korunan karar",message.Body);
        await store.UpdateInstitutionStudentAsync(s.Admin,s.Institution,s.Student,9,s.Next,default);Assert.Equal(1,await db.Set<OutboxMessage>().CountAsync());
        s.User.UserId=s.Next;var reviews=DecisionResponsibilityAndPostponementTests.Reviews(db,s.User,s.Accounts,publisher);
        var request=new Coaching.Application.StudyPlanning.WeeklyDecisionPostpone(transferred.Version,new(2026,10,28),"Gerekçeli erteleme");
        failure.Enabled=true;await Assert.ThrowsAsync<InvalidOperationException>(()=>reviews.PostponeAsync(s.Student,week,transferred.Id,request,default));
        Assert.Equal(transferred.DueOn,(await db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).DueOn);Assert.Equal(1,await db.Set<OutboxMessage>().CountAsync());
        failure.Enabled=false;var saved=await reviews.PostponeAsync(s.Student,week,transferred.Id,request,default);Assert.Equal(request.DueOn,saved.DueOn);Assert.Equal(2,await db.Set<OutboxMessage>().CountAsync());
        await reviews.PostponeAsync(s.Student,week,transferred.Id,request,default);Assert.Equal(2,await db.Set<OutboxMessage>().CountAsync());
        var future=new Coaching.Application.StudyPlanning.WeeklyDecisionPostpone(saved.Version,new(2026,10,30),"Gelecek tarihli takvim değişikliği");
        failure.Enabled=true;await Assert.ThrowsAsync<InvalidOperationException>(()=>reviews.ChangeDeadlineAsync(s.Student,week,saved.Id,future,default));
        Assert.Equal(saved.DueOn,(await db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).DueOn);Assert.Equal(2,await db.Set<OutboxMessage>().CountAsync());
        failure.Enabled=false;await reviews.ChangeDeadlineAsync(s.Student,week,saved.Id,future,default);
        Assert.Equal(future.DueOn,(await db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).DueOn);Assert.Equal(3,await db.Set<OutboxMessage>().CountAsync());
        await reviews.ChangeDeadlineAsync(s.Student,week,saved.Id,future,default);Assert.Equal(3,await db.Set<OutboxMessage>().CountAsync());
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task InvitationHandoffFailureCanRetryInTheSameScopeWithoutLosingThePendingInvitation()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var s=await DecisionResponsibilityAndPostponementTests.Seed(pg.Db,false);
        var week=new DateOnly(2026,10,5);await DecisionResponsibilityAndPostponementTests.Reviews(pg.Db,s.User,s.Accounts).SaveDecisionAsync(s.Student,week,Guid.NewGuid(),new(null,"Açık karar","",week.AddDays(10),"Planned"),default);
        var failure=new Failure();var services=new ServiceCollection();services.AddLogging();
        services.AddDbContext<CoachingDbContext>(o=>o.UseNpgsql(pg.Db.Database.GetConnectionString()).AddInterceptors(failure));
        services.AddMassTransit(x=>{x.AddEntityFrameworkOutbox<CoachingDbContext>(o=>{o.UsePostgres();o.UseBusOutbox();});x.UsingInMemory((ctx,cfg)=>cfg.ConfigureEndpoints(ctx));});
        await using var provider=services.BuildServiceProvider();await using var scope=provider.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<CoachingDbContext>();
        var publisher=new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>());var clock=new DecisionResponsibilityAndPostponementTests.Clock();
        var profiles=new CoachingProfileStore(db,s.Accounts,publisher,clock);await profiles.EndAssignmentAsync(s.Old,s.Old,s.Student,false,default);// O12: the student is told (one outbox message).
        var invites=new CoachingInvitationStore(db,s.Accounts,profiles,clock,publisher,new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var invitation=await invites.SendTeacherAsync(s.Next,"test@example.test",null,default);Assert.Equal(2,await db.Set<OutboxMessage>().CountAsync());
        failure.Enabled=true;await Assert.ThrowsAsync<InvalidOperationException>(()=>invites.AcceptAsync(s.Student,invitation.Id,default));
        Assert.Equal(Coaching.Domain.Profiles.CoachingInvitationStatus.Pending,(await db.CoachingInvitations.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(s.Old,(await db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).ResponsibleTeacherId);Assert.Equal(2,await db.Set<OutboxMessage>().CountAsync());
        failure.Enabled=false;await invites.AcceptAsync(s.Student,invitation.Id,default);Assert.Equal(s.Next,(await db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).ResponsibleTeacherId);
        Assert.Equal(3,await db.Set<OutboxMessage>().CountAsync());await invites.AcceptAsync(s.Student,invitation.Id,default);Assert.Equal(3,await db.Set<OutboxMessage>().CountAsync());
    }

    private sealed class Failure:SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(Enabled&&data.Context!.ChangeTracker.Entries<OutboxMessage>().Any(x=>x.State==EntityState.Added))throw new InvalidOperationException("Outbox failure");
            return ValueTask.FromResult(result);
        }
    }
}
