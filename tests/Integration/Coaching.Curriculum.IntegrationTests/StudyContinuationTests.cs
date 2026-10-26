using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class StudyContinuationTests
{
    private static readonly WeeklyOutcomePlanRequest Input=new(new(2027,5,24),new(2027,6,14),0,false);
    private static CoachingManualStudyPlanService Manual(CurriculumManagementTests.Fixture f) {
        var policy=new CoachingAccessPolicy(f.User);return new(f.Db,policy,new CoachingAutomaticStudyPlanPreviewService(f.Db,policy),new CurriculumManagementTests.NoopPublisher());
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task PartialScopeAndOpenDecisionSurviveRepeatedAnnualRenewalsAndOnlyCompletionGrantsCurriculumCredit()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var planner=await WeeklyOutcomePlanTests.Seed(f);
        var active=await f.Db.StudyPlanRevisions.SingleAsync();var topic=await f.Db.StudyCatalogTopics.SingleAsync();var outcome=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();var estimate=await f.Db.Set<OutcomeDurationEstimate>().SingleAsync();
        var source=StudyPlanTask.Create(active,new(2026,10,19),"Başlanmış çalışma",60,topic.Id,plannedQuestions:30);
        source.RecordRealization(20,8,false,active.StudentId,remainingMinutes:25,remainingQuestions:12);
        var decision=WeeklyDecision.Create(Guid.NewGuid(),active.StudentId,Guid.NewGuid(),new(2026,10,12));decision.Edit("Devam kararı","İlk not",new(2026,10,20),"InProgress",DateTime.UtcNow);decision.LinkWork(source.Id,null,DateTime.UtcNow);
        f.Db.AddRange(source,decision,StudyPlanTaskOutcome.Create(source.Id,outcome.Id,estimate.Id,30,"Catalog"));await f.Db.SaveChangesAsync();
        var original=JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==source.Id));var history=decision.History.ToArray();
        var preview=await planner.PreviewAsync(Input,default);Assert.True(preview.CanCreate);Assert.Empty(preview.Schedule.Tasks);
        Assert.Equal(25,preview.Schedule.Lessons.Single().RequiredMinutes);Assert.Equal(25,preview.ProtectedTasks!.Single().Minutes);
        var created=await planner.CreateAsync(new("Devam planı",Input,preview.Fingerprint),default);var manual=Manual(f);
        var published=await manual.PublishAsync(created.Id,created.Version);
        var copy=Assert.Single(published.Tasks);Assert.Equal(25,copy.PlannedMinutes);Assert.Equal(12,copy.PlannedQuestions);Assert.Null(copy.ActualMinutes);
        Assert.Equal(source.Id,copy.OriginalTaskId);Assert.Equal(source.Id,copy.PredecessorTaskId);
        var linked=await f.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync();Assert.Equal(copy.Id,linked.StudyTaskId);Assert.Equal(history,linked.History.Take(history.Length));
        Assert.Equal("WorkContinuation",linked.History.Last().Action);Assert.Equal(source.Id,linked.History.Last().PreviousStudyTaskId);
        Assert.Equal(original,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==source.Id)));
        // A continuation with no new effort remains protected even when it was never pinned.
        preview=await planner.PreviewAsync(Input,default);Assert.Empty(preview.Schedule.Tasks);Assert.Equal(25,preview.ProtectedTasks!.Single().Minutes);
        created=await planner.CreateAsync(new("İkinci devam",Input,preview.Fingerprint),default);published=await manual.PublishAsync(created.Id,created.Version);
        var next=Assert.Single(published.Tasks);Assert.Equal(copy.Id,next.PredecessorTaskId);Assert.Equal(source.Id,next.OriginalTaskId);Assert.Equal(25,next.PlannedMinutes);
        linked=await f.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync();Assert.Equal(next.Id,linked.StudyTaskId);Assert.Equal(2,linked.History.Count(x=>x.Action=="WorkContinuation"));
        await manual.CompleteTaskAsync(published.Id,next.Id,published.Version,22,12);
        preview=await planner.PreviewAsync(Input,default);Assert.Empty(preview.Schedule.Tasks);Assert.Equal(0,preview.Schedule.Lessons.Single().RequiredMinutes);
        Assert.Equal(30,(await f.Db.Set<StudyPlanTaskOutcome>().SingleAsync(x=>x.TaskId==next.Id)).AllocatedMinutes);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task LegacyPartialRequiresExplicitScopeAndChangedScopeInvalidatesAnUnpublishedContinuation()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var planner=await WeeklyOutcomePlanTests.Seed(f);var active=await f.Db.StudyPlanRevisions.SingleAsync();var topic=await f.Db.StudyCatalogTopics.SingleAsync();
        var source=StudyPlanTask.Create(active,new(2026,10,19),"Eski kısmi",60,topic.Id);source.RecordRealization(20,5,false,active.StudentId);f.Db.Add(source);await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(()=>planner.PreviewAsync(Input,default));Assert.Null(source.RemainingMinutes);Assert.Single(await f.Db.StudyPlanRevisions.ToArrayAsync());
        var manual=Manual(f);var view=await manual.RecordRealizationAsync(active.Id,source.Id,new(active.Version,20,5,false,RemainingMinutes:25),false);
        var preview=await planner.PreviewAsync(Input,default);var created=await planner.CreateAsync(new("Taslak",Input,preview.Fingerprint),default);
        await manual.RecordRealizationAsync(active.Id,source.Id,new(view.Version,20,5,false,RemainingMinutes:15),false);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>manual.PublishAsync(created.Id,created.Version));
        Assert.Equal(active.Id,(await f.Db.StudyPlanRevisions.SingleAsync(x=>x.IsActive)).Id);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ManualReplacementKeepsUnpinnedDecisionWorkAndHistoryAndRejectsDeletingContinuation()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();var active=await f.Db.StudyPlanRevisions.SingleAsync();f.User.UserId=active.StudentId;f.User.Roles=["Student"];
        var source=StudyPlanTask.Create(active,new(2026,10,19),"Karar işi",30);source.Schedule(source.PlannedDate,1080);
        var decision=WeeklyDecision.Create(Guid.NewGuid(),active.StudentId,Guid.NewGuid(),new(2026,10,12));decision.Edit("Karar","",new(2026,10,20),"Planned",DateTime.UtcNow);decision.LinkWork(source.Id,null,DateTime.UtcNow);
        f.Db.AddRange(source,decision);await f.Db.SaveChangesAsync();var old=JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==source.Id));var manual=Manual(f);
        var draft=await manual.CreateDraftAsync(new("Elle yeni plan",[new(new(2026,10,26),"Yeni iş",30,null,false,PlannedStartMinute:1080)]));
        var copy=Assert.Single(draft.Tasks.Where(x=>x.PredecessorTaskId==source.Id));Assert.NotNull(copy.PlannedStartMinute);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>manual.DeleteCalendarTaskAsync(draft.Id,copy.Id,draft.Version));
        var result=await manual.PublishAsync(draft.Id,draft.Version);
        Assert.Equal(copy.Id,(await f.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).StudyTaskId);
        Assert.Equal(old,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==source.Id)));
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task AutomaticRenewalReservesOnlyRemainingTimeAndKeepsFullOutcomeAllocationWithoutDuplicatingWork()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();var active=await f.Db.StudyPlanRevisions.SingleAsync();var student=active.StudentId;f.User.UserId=student;f.User.Roles=["Student"];
        var topic=await f.Db.StudyCatalogTopics.SingleAsync();var estimate=await f.Db.Set<OutcomeDurationEstimate>().SingleAsync();var outcome=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();
        var source=StudyPlanTask.Create(active,new(2026,10,19),"Kısmi konu",60,topic.Id);source.RecordRealization(20,8,false,student,remainingMinutes:40);source.Schedule(source.PlannedDate,1080);
        var availability=StudyAvailability.Create(student,"Europe/Istanbul");availability.ReplacePreferences("Europe/Istanbul",[new(DayOfWeek.Monday,1080,1200)]);
        f.Db.AddRange(source,availability,StudyPlanTaskOutcome.Create(source.Id,outcome.Id,estimate.Id,60,"Catalog"));await f.Db.SaveChangesAsync();
        var policy=new CoachingAccessPolicy(f.User);var previews=new CoachingAutomaticStudyPlanPreviewService(f.Db,policy);
        var input=new AutomaticStudyPreviewRequest(new(2026,10,26),1,availability.Version,[new(topic.Id,null,[new(outcome.Id,estimate.Id,null)])]);
        var preview=await previews.PreviewAsync(input);Assert.Empty(preview.Schedule.Tasks);Assert.Equal(40,preview.ProtectedTasks.Single().PlannedMinutes);
        var manual=Manual(f);var draft=await manual.CreateAutomaticDraftAsync(new("Otomatik devam",input,preview.ActiveRevisionId,preview.ActiveRevisionVersion));
        var copy=Assert.Single(draft.Tasks);Assert.Equal(40,copy.PlannedMinutes);Assert.Equal(source.Id,copy.PredecessorTaskId);Assert.Null(copy.ActualMinutes);
        Assert.Equal(60,copy.Outcomes!.Single().AllocatedMinutes);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TimedPinnedManualTaskWithoutLessonKeepsItsClockAndFieldsAcrossAnnualAutomaticAndManualPlans()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var annual=await WeeklyOutcomePlanTests.Seed(f);
        var active=await f.Db.StudyPlanRevisions.SingleAsync();var student=active.StudentId;
        var source=StudyPlanTask.Create(active,Input.StartDate,"Saatli manuel çalışma",25,isPinned:true,sourceName:"Soru kitabı",plannedQuestions:15);
        source.Schedule(Input.StartDate,1140);f.Db.Add(source);await f.Db.SaveChangesAsync();
        var original=JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==source.Id));
        var preview=await annual.PreviewAsync(Input,default);Assert.True(preview.CanCreate);
        var annualPlan=await annual.CreateAsync(new("Yıllık plan",Input,preview.Fingerprint),default);var manual=Manual(f);
        var published=await manual.PublishAsync(annualPlan.Id,annualPlan.Version);
        var first=Assert.Single(published.Tasks.Where(x=>x.PredecessorTaskId==source.Id));Check(first);Assert.Equal(source.Id,first.OriginalTaskId);
        var availability=StudyAvailability.Create(student,"Europe/Istanbul");availability.ReplacePreferences("Europe/Istanbul",[new(DayOfWeek.Monday,1080,1200)]);
        f.Db.Add(availability);await f.Db.SaveChangesAsync();var topic=await f.Db.StudyCatalogTopics.SingleAsync();
        var automatic=new AutomaticStudyPreviewRequest(Input.StartDate,1,availability.Version,[new(topic.Id,30)]);
        var policy=new CoachingAccessPolicy(f.User);var autoPreview=await new CoachingAutomaticStudyPlanPreviewService(f.Db,policy).PreviewAsync(automatic);
        var auto=await manual.CreateAutomaticDraftAsync(new("Otomatik plan",automatic,autoPreview.ActiveRevisionId,autoPreview.ActiveRevisionVersion));
        var second=Assert.Single(auto.Tasks.Where(x=>x.PredecessorTaskId==first.Id));Check(second);Assert.Equal(source.Id,second.OriginalTaskId);
        await manual.PublishAsync(auto.Id,auto.Version);
        var next=await manual.CreateDraftAsync(new("Elle plan",[new(Input.StartDate,"Yeni iş",30,null,false,PlannedStartMinute:1080)]));
        var third=Assert.Single(next.Tasks.Where(x=>x.PredecessorTaskId==second.Id));Check(third);Assert.Equal(source.Id,third.OriginalTaskId);
        await manual.PublishAsync(next.Id,next.Version);
        Assert.Equal(original,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==source.Id)));
        Assert.Equal(1140,(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==first.Id)).PlannedStartMinute);
        static void Check(ManualStudyTaskView task) {
            Assert.Equal(1140,task.PlannedStartMinute);Assert.Null(task.WeeklyLessonId);Assert.Null(task.LessonName);
            Assert.Equal(Input.StartDate,task.PlannedDate);Assert.Equal(25,task.PlannedMinutes);Assert.Equal(15,task.PlannedQuestions);
            Assert.Equal("Soru kitabı",task.SourceName);Assert.Equal("Saatli manuel çalışma",task.Title);Assert.True(task.IsPinned);Assert.Null(task.ActualMinutes);
        }
    }
    [Fact]
    public void ReducedContinuationPreservesTheOriginalSameDayPlanDenominatorAndDoesNotDuplicateEffort()
    {
        var date=new DateOnly(2026,10,19);var root=Guid.NewGuid();
        var report=StudyReportCalculator.Calculate(date,date,[
            new(StudyPlanStatus.Archived,date,null,60,false,20,30,8,true,root),
            new(StudyPlanStatus.Active,date,null,25,true,22,12,12,true,root)]);
        Assert.Equal(1,report.ScheduledTasks);Assert.Equal(1,report.CompletedTasks);Assert.Equal(60,report.PlannedMinutes);
        Assert.Equal(30,report.PlannedQuestions);Assert.Equal(42,report.ActualMinutes);Assert.Equal(20,report.ActualQuestions);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task PublicationAndDecisionContinuationRollbackTogetherWithOutboxAndRetryOnce()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();var original=await f.Db.StudyPlanRevisions.SingleAsync();var student=original.StudentId;f.User.UserId=student;f.User.Roles=["Student"];
        var task=StudyPlanTask.Create(original,new(2026,10,19),"Karar işi",30);
        var decision=WeeklyDecision.Create(Guid.NewGuid(),student,Guid.NewGuid(),new(2026,10,12));decision.Edit("Karar","",new(2026,10,20),"Planned",DateTime.UtcNow);decision.LinkWork(task.Id,null,DateTime.UtcNow);
        f.Db.AddRange(task,decision);await f.Db.SaveChangesAsync();var before=decision.History.Count;
        var draft=await Manual(f).CreateDraftAsync(new("Yeni plan",[new(new(2026,10,26),"Yeni çalışma",30,null,false)]));
        var failure=new OutboxFailure();var services=new ServiceCollection();services.AddLogging();
        services.AddDbContext<CoachingDbContext>(x=>x.UseNpgsql(f.Db.Database.GetConnectionString()).AddInterceptors(failure));
        services.AddMassTransit(x=>{x.AddEntityFrameworkOutbox<CoachingDbContext>(o=>{o.UsePostgres();o.UseBusOutbox();});x.UsingInMemory((ctx,cfg)=>cfg.ConfigureEndpoints(ctx));});
        await using var provider=services.BuildServiceProvider();await using var scope=provider.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<CoachingDbContext>();
        var policy=new CoachingAccessPolicy(f.User);var publisher=new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>());
        var manual=new CoachingManualStudyPlanService(db,policy,new CoachingAutomaticStudyPlanPreviewService(db,policy),publisher);
        failure.Enabled=true;await Assert.ThrowsAsync<InvalidOperationException>(()=>manual.PublishAsync(draft.Id,draft.Version));
        Assert.Equal(original.Id,(await db.StudyPlanRevisions.AsNoTracking().SingleAsync(x=>x.IsActive)).Id);
        var saved=await db.Set<WeeklyDecision>().AsNoTracking().SingleAsync();Assert.Equal(task.Id,saved.StudyTaskId);Assert.Equal(before,saved.History.Count);
        Assert.Empty(await db.Set<OutboxMessage>().AsNoTracking().ToArrayAsync());
        failure.Enabled=false;await manual.PublishAsync(draft.Id,draft.Version);
        saved=await db.Set<WeeklyDecision>().AsNoTracking().SingleAsync();Assert.NotEqual(task.Id,saved.StudyTaskId);Assert.Equal(before+1,saved.History.Count);
        Assert.Equal(2,await db.Set<OutboxMessage>().CountAsync());
    }
    private sealed class OutboxFailure:SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(Enabled&&data.Context!.ChangeTracker.Entries<OutboxMessage>().Any(x=>x.State==EntityState.Added))throw new InvalidOperationException("Simulated outbox failure");
            return ValueTask.FromResult(result);
        }
    }
    [Fact]
    public void ScopeDeclarationDoesNotFabricateRealizationAndCompletionKeepsScopeHistory()
    {
        var student=Guid.NewGuid();var plan=StudyPlanRevision.Create(student,Guid.NewGuid(),1,"Plan");var task=StudyPlanTask.Create(plan,new(2026,10,19),"İş",60);
        task.DeclareRemainingScope(40,12,student);Assert.Null(task.ActualMinutes);Assert.Null(task.ActualQuestions);Assert.False(task.IsCompleted);
        Assert.Equal(40,task.RemainingMinutes);Assert.Equal("RemainingScope",Assert.Single(task.RealizationHistory).Action);
        task.DeclareRemainingScope(40,12,student);Assert.Single(task.RealizationHistory);
        task.Complete(35,12);Assert.Null(task.RemainingMinutes);Assert.True(task.IsCompleted);Assert.Equal(40,task.RealizationHistory.Last().PreviousRemainingMinutes);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task LegacyContinuationCanDeclareScopeWithoutRewritingItsPredecessorActuals()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var planner=await WeeklyOutcomePlanTests.Seed(f);var active=await f.Db.StudyPlanRevisions.SingleAsync();var topic=await f.Db.StudyCatalogTopics.SingleAsync();var outcome=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();var estimate=await f.Db.Set<OutcomeDurationEstimate>().SingleAsync();
        var old=StudyPlanRevision.Create(active.StudentId,Guid.NewGuid(),1,"Eski geçmiş");old.Activate();
        var source=StudyPlanTask.Create(old,new(2026,10,19),"Eski kısmi",60,topic.Id);source.RecordRealization(20,8,false,active.StudentId);old.Archive();
        var legacy=StudyPlanTask.Create(active,Input.StartDate,"Eski yöntemle devreden",60,topic.Id,true);legacy.CarryFrom(source);legacy.Schedule(Input.StartDate,1080);
        f.Db.AddRange(old,source,legacy,StudyPlanTaskOutcome.Create(legacy.Id,outcome.Id,estimate.Id,30,"Catalog"));await f.Db.SaveChangesAsync();
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE coaching.study_plan_tasks SET "PredecessorTaskId"=NULL,"PredecessorVersion"=NULL WHERE "Id"={legacy.Id}""");f.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<BusinessRuleException>(()=>planner.PreviewAsync(Input,default));
        var before=JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==source.Id));
        var version=(await f.Db.StudyPlanRevisions.AsNoTracking().SingleAsync(x=>x.Id==active.Id)).Version;
        var view=await Manual(f).SetRemainingScopeAsync(active.Id,legacy.Id,new(version,25,12));
        Assert.Null(view.Tasks.Single(x=>x.Id==legacy.Id).ActualMinutes);
        var preview=await planner.PreviewAsync(Input,default);Assert.Empty(preview.Schedule.Tasks);Assert.Equal(25,preview.ProtectedTasks!.Single().Minutes);
        Assert.Equal(before,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==source.Id)));
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task AdditiveMigrationPreservesOldTaskColumnsAndDoesNotGuessRemainingScope()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create("20261024213209_WeeklyReviewResultEvidence");await pg.Data.Seed(legacyPlanSchema:true);
        async Task<string> Row()=>await pg.Db.Database.SqlQueryRaw<string>("""SELECT (to_jsonb(t)-'RemainingMinutes'-'RemainingQuestions'-'PredecessorTaskId'-'PredecessorVersion'-'ActualWorkJson'-'WorkFromDate'-'WorkToDate'-'CompletionSourceTaskId')::text AS "Value" FROM coaching.study_plan_tasks t""").SingleAsync();
        var before=await Row();await pg.Db.Database.MigrateAsync();Assert.Equal(before,await Row());Assert.False(pg.Db.Database.HasPendingModelChanges());
        var task=await pg.Db.StudyPlanTasks.SingleAsync();Assert.Null(task.RemainingMinutes);Assert.Null(task.PredecessorTaskId);
    }
}
