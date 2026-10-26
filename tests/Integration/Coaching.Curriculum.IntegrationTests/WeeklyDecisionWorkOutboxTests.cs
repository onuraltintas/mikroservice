using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Messaging;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class WeeklyDecisionWorkOutboxTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task SecondSaveFailureRollsBackCalendarAndAssignmentAndTheirLinksAndMessagesThenSameScopeRetriesOnce()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();await pg.Data.Seed();var student=(await pg.Db.StudyPlanRevisions.SingleAsync()).StudentId;var teacher=Guid.NewGuid();
        await WeeklyReviewPostgresTests.Setup(pg,student,teacher);var user=pg.Data.User;user.UserId=teacher;user.Roles=["Teacher"];
        var failure=new Failure();var services=new ServiceCollection();services.AddLogging();
        services.AddDbContext<CoachingDbContext>(o=>o.UseNpgsql(pg.Db.Database.GetConnectionString()).AddInterceptors(failure));
        services.AddMassTransit(x=>{x.AddEntityFrameworkOutbox<CoachingDbContext>(o=>{o.UsePostgres();o.UseBusOutbox();});x.UsingInMemory((ctx,cfg)=>cfg.ConfigureEndpoints(ctx));});
        await using var provider=services.BuildServiceProvider();await using var scope=provider.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<CoachingDbContext>();
        var policy=new CoachingAccessPolicy(user);var directory=new OwnedCoachingDirectory(db,new WeeklyFollowUpPostgresTests.Accounts(teacher),user);
        var service=new CoachingWeeklyReviewService(db,policy,new(policy,directory,new CurriculumManagementTests.Scope()),directory,new WeeklyFollowUpPostgresTests.Clock(),new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()));
        var week=new DateOnly(2026,10,12);var id=Guid.NewGuid();var row=await service.SaveDecisionAsync(student,week,id,new(null,"Tekrar","",week.AddDays(10),"Planned"),default);
        var plan=await db.StudyPlanRevisions.AsNoTracking().SingleAsync();
        var proposal=new DecisionStudyProposal(row.Version,plan.Version,plan.Id,null,new(2026,10,22),1080,30);var preview=await service.PreviewDecisionStudyAsync(student,week,id,proposal,default);
        var request=new DecisionStudyApply(proposal,preview.Fingerprint,true,Guid.NewGuid());failure.Enabled=true;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ApplyDecisionStudyAsync(student,week,id,request,default));
        Assert.Equal(1,await db.StudyPlanTasks.CountAsync());Assert.Null((await db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).StudyTaskId);Assert.Empty(await db.Set<IdempotencyRecord>().ToArrayAsync());
        Assert.Equal(plan.Version,(await db.StudyPlanRevisions.AsNoTracking().SingleAsync()).Version);Assert.Equal(1,await db.Set<OutboxMessage>().CountAsync());
        failure.Enabled=false;row=await service.ApplyDecisionStudyAsync(student,week,id,request,default);Assert.Equal(2,await db.StudyPlanTasks.CountAsync());Assert.Equal(3,await db.Set<OutboxMessage>().CountAsync());
        await service.ApplyDecisionStudyAsync(student,week,id,request,default);Assert.Equal(3,await db.Set<OutboxMessage>().CountAsync());
        var assignment=new DecisionAssignmentCreate(row.Version,new(2026,10,23,12,0,0,DateTimeKind.Utc),Guid.NewGuid(),30);failure.Enabled=true;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.CreateDecisionAssignmentAsync(student,week,id,assignment,default));
        Assert.Empty(await db.Assignments.ToArrayAsync());Assert.Null((await db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).AssignmentId);Assert.Equal(3,await db.Set<OutboxMessage>().CountAsync());
        failure.Enabled=false;await service.CreateDecisionAssignmentAsync(student,week,id,assignment,default);
        Assert.Equal(1,await db.Assignments.CountAsync());Assert.Equal(5,await db.Set<OutboxMessage>().CountAsync());Assert.Equal(2,await db.Set<IdempotencyRecord>().CountAsync());
    }
    private sealed class Failure:SaveChangesInterceptor {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default){
            if(Enabled&&data.Context!.ChangeTracker.Entries<OutboxMessage>().Any(x=>x.State==EntityState.Added&&x.Entity.MessageType.Contains("WeeklyDecisionChangedEvent")))throw new InvalidOperationException("Simulated second-save outbox failure");
            return ValueTask.FromResult(result);
        }
    }
}
