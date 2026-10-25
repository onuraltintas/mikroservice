using Coaching.Application.Authorization;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Messaging;
using Coaching.Infrastructure.Privacy;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using Coaching.Domain.Entities;
using EduPlatform.Shared.Contracts.Events.Privacy;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class WeeklyFollowUpOutboxTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task RolledBackPublicationDoesNotLeaveRecordOrNotificationAndSameScopeCanRetry()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();await pg.Data.Seed();var student=Guid.NewGuid();var teacher=Guid.NewGuid();
        await WeeklyReviewPostgresTests.Setup(pg,student,teacher);var user=pg.Data.User;user.UserId=student;user.Roles=["Student"];
        var taskBefore=System.Text.Json.JsonSerializer.Serialize(await pg.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync());
        var failure=new Failure();var services=new ServiceCollection();services.AddLogging();
        services.AddDbContext<CoachingDbContext>(x=>x.UseNpgsql(pg.Db.Database.GetConnectionString()).AddInterceptors(failure));
        services.AddMassTransit(x=>{x.AddEntityFrameworkOutbox<CoachingDbContext>(o=>{o.UsePostgres();o.UseBusOutbox();});x.UsingInMemory((ctx,cfg)=>cfg.ConfigureEndpoints(ctx));});
        await using var provider=services.BuildServiceProvider();await using var scope=provider.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<CoachingDbContext>();
        var policy=new CoachingAccessPolicy(user);var directory=new OwnedCoachingDirectory(db,new WeeklyFollowUpPostgresTests.Accounts(teacher),user);
        var service=new CoachingWeeklyReviewService(db,policy,new(policy,directory,new CurriculumManagementTests.Scope()),directory,new WeeklyFollowUpPostgresTests.Clock(),new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()));
        var assessor=new CoachingErasureAssessmentService(db,TimeProvider.System);
        async Task<int> Count(Guid subject)=>(await assessor.AssessAsync(new(Guid.NewGuid(),Guid.NewGuid(),subject,DateTime.UtcNow,true,PersonalDataScope.Coaching),default)).StudyPlanningRecordCount;
        var studentBefore=await Count(student);var teacherBefore=await Count(teacher);var week=new DateOnly(2026,10,12);
        failure.Enabled=true;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SaveReflectionAsync(week,new(null,"Özel değerlendirme","","Tekrar",4),default));
        Assert.Empty(await db.Set<WeeklyReflection>().AsNoTracking().ToArrayAsync());Assert.Empty(await db.Set<OutboxMessage>().AsNoTracking().ToArrayAsync());
        failure.Enabled=false;
        var saved=await service.SaveReflectionAsync(week,new(null,"Özel değerlendirme","","Tekrar",4),default);
        var message=Assert.Single(await db.Set<OutboxMessage>().AsNoTracking().ToArrayAsync());Assert.Contains("WeeklyReflectionSavedEvent",message.MessageType);Assert.DoesNotContain("Özel değerlendirme",message.Body);
        Assert.Equal(studentBefore+2,await Count(student));Assert.Equal(teacherBefore+1,await Count(teacher));
        await service.SaveReflectionAsync(week,new(saved.Version,"Özel değerlendirme","","Tekrar",4),default);Assert.Equal(1,await db.Set<OutboxMessage>().CountAsync());
        user.UserId=teacher;user.Roles=["Teacher"];var id=Guid.NewGuid();failure.Enabled=true;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SaveDecisionAsync(student,week,id,new(null,"Karar","",week.AddDays(7),"Planned"),default));
        Assert.Empty(await db.Set<WeeklyDecision>().AsNoTracking().ToArrayAsync());Assert.Equal(1,await db.Set<OutboxMessage>().CountAsync());
        failure.Enabled=false;await service.SaveDecisionAsync(student,week,id,new(null,"Karar","",week.AddDays(7),"Planned"),default);
        Assert.Equal(2,await db.Set<OutboxMessage>().CountAsync());
        Assert.Equal(taskBefore,System.Text.Json.JsonSerializer.Serialize(await pg.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync()));
        Assert.False(db.Database.HasPendingModelChanges());
    }
    private sealed class Failure:SaveChangesInterceptor {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default) {
            if(Enabled && data.Context!.ChangeTracker.Entries<OutboxMessage>().Any(x=>x.State==EntityState.Added))throw new InvalidOperationException("Simulated outbox write failure");
            return ValueTask.FromResult(result);
        }
    }
}
