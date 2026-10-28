using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class StudyProgressReconciliationTests
{
    private static readonly DateOnly Day=new(2026,10,19);
    private static readonly WeeklyOutcomePlanRequest Annual=new(new(2027,5,24),new(2027,6,14),0,false);
    private static CoachingManualStudyPlanService Manual(CurriculumManagementTests.Fixture f)
    {
        var access=new CoachingAccessPolicy(f.User);
        return new(f.Db,access,new CoachingAutomaticStudyPlanPreviewService(f.Db,access),new CurriculumManagementTests.NoopPublisher());
    }
    private static async Task<(StudyPlanRevision Old,StudyPlanTask Source,ManualStudyPlanView Active,Guid Copy)> Seed(CurriculumManagementTests.Fixture f)
    {
        await WeeklyOutcomePlanTests.Seed(f);
        var old=await f.Db.StudyPlanRevisions.SingleAsync();var topic=await f.Db.StudyCatalogTopics.SingleAsync();
        var source=StudyPlanTask.Create(old,Day,"Kısmi çalışma",60,topic.Id,plannedQuestions:30);
        source.RecordRealization(20,8,false,old.StudentId,remainingMinutes:40,remainingQuestions:12,workedOn:Day);
        var outcome=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();var estimate=await f.Db.Set<OutcomeDurationEstimate>().SingleAsync();
        var decision=WeeklyDecision.Create(Guid.NewGuid(),old.StudentId,Guid.NewGuid(),Day.AddDays(-7));
        decision.Edit("Devam et","",Day.AddDays(1),"InProgress",DateTime.UtcNow);decision.LinkWork(source.Id,null,DateTime.UtcNow);
        f.Db.AddRange(source,decision,StudyPlanTaskOutcome.Create(source.Id,outcome.Id,estimate.Id,30,"Catalog"));await f.Db.SaveChangesAsync();
        var service=Manual(f);var draft=await service.CreateDraftAsync(new("Devam planı",[new(Day.AddDays(7),"Başka çalışma",30,null,false,PlannedStartMinute:1080)]));
        var active=await service.PublishAsync(draft.Id,draft.Version);
        return (old,source,active,active.Tasks.Single(x=>x.PredecessorTaskId==source.Id).Id);
    }
    private static Task<StudyPlanTask> TaskRow(CurriculumManagementTests.Fixture f,Guid id)
        =>f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==id);
    private static Task<StudyPlanRevision> PlanRow(CurriculumManagementTests.Fixture f,Guid id)
        =>f.Db.StudyPlanRevisions.AsNoTracking().SingleAsync(x=>x.Id==id);
    private static string Effort(StudyPlanTask task)=>JsonSerializer.Serialize(new {task.ActualMinutes,task.ActualQuestions,task.ActualWorkJson,task.WorkFromDate,task.WorkToDate});
    private static async Task<WeeklyOutcomePlanPreview> Preview(CurriculumManagementTests.Fixture f)
        =>await new CoachingWeeklyOutcomePlanService(f.Db,new CoachingAccessPolicy(f.User),new WeeklyStudyDraftTests.Catalog(f.Db),TimeProvider.System).PreviewAsync(Annual,default);

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task RestoringAncestorAndThenDescendantPreservesLatestRemainingScopeAndEveryEffortLedger()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var (old,source,active,copy)=await Seed(f);var service=Manual(f);
        active=await service.RecordRealizationAsync(active.Id,copy,new(active.Version,10,4,false,RemainingMinutes:15,RemainingQuestions:5,WorkedOn:Day.AddDays(1)),false);
        var sourceBefore=await TaskRow(f,source.Id);var copyBefore=JsonSerializer.Serialize(await TaskRow(f,copy));
        var archived=await PlanRow(f,old.Id);
        var restored=await service.RestoreArchivedAsync(old.Id,new(archived.Version,active.Id,active.Version));
        var task=restored.Tasks.Single(x=>x.Id==source.Id);
        Assert.Equal(60,task.PlannedMinutes);Assert.Equal(30,task.PlannedQuestions);Assert.Equal(15,task.RemainingMinutes);Assert.Equal(5,task.RemainingQuestions);
        Assert.Equal(Effort(sourceBefore),Effort(await TaskRow(f,source.Id)));
        Assert.Equal(JsonSerializer.Serialize(sourceBefore.RealizationHistory),JsonSerializer.Serialize((await TaskRow(f,source.Id)).RealizationHistory.Take(sourceBefore.RealizationHistory.Count)));
        Assert.Equal(copyBefore,JsonSerializer.Serialize(await TaskRow(f,copy)));
        Assert.Equal(source.Id,(await f.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).StudyTaskId);
        var next=await PlanRow(f,active.Id);
        restored=await service.RestoreArchivedAsync(next.Id,new(next.Version,restored.Id,restored.Version));
        Assert.Equal(15,restored.Tasks.Single(x=>x.Id==copy).RemainingMinutes);
        Assert.Equal(15,(await Preview(f)).ProtectedTasks!.Single(x=>x.Title==source.Title).Minutes);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task RestoringCompletedContinuationDoesNotReopenWorkOrCopyItsEffortAndRetainsFullAllocation()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var (old,source,active,copy)=await Seed(f);var service=Manual(f);
        active=await service.RecordRealizationAsync(active.Id,copy,new(active.Version,22,12,true,WorkedOn:Day.AddDays(1)),false);
        var sourceBefore=await TaskRow(f,source.Id);var completed=await TaskRow(f,copy);var archived=await PlanRow(f,old.Id);
        var restored=await service.RestoreArchivedAsync(old.Id,new(archived.Version,active.Id,active.Version));
        var task=restored.Tasks.Single(x=>x.Id==source.Id);
        Assert.True(task.IsCompleted);Assert.Equal(copy,task.CompletionSourceTaskId);Assert.Equal(completed.CompletedAt,task.CompletedAt);
        Assert.Null(task.RemainingMinutes);Assert.Equal(Effort(sourceBefore),Effort(await TaskRow(f,source.Id)));
        Assert.Equal("ContinuationReconciliation",task.RealizationHistory!.Last().Action);
        Assert.Equal(source.Id,(await f.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).StudyTaskId);
        Assert.Equal(0,(await Preview(f)).Schedule.Lessons.Single().RequiredMinutes);
        var rows=await f.Db.StudyPlanTasks.AsNoTracking().Where(x=>x.Id==source.Id||x.OriginalTaskId==source.Id).ToArrayAsync();
        var report=StudyReportCalculator.Calculate(Day,Day.AddDays(6),rows.Select(x=>new StudyReportTask(x.RevisionId==old.Id?StudyPlanStatus.Active:StudyPlanStatus.Archived,x.PlannedDate,x.TopicId,x.PlannedMinutes,x.IsCompleted,x.ActualMinutes,WasPublished:true,WorkId:source.Id,WorkDays:x.WorkDays)).ToArray());
        Assert.Equal(42,report.ActualMinutes);Assert.Equal(30,task.Outcomes!.Single().AllocatedMinutes);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task HistoricalCompletionClosesUntouchedContinuationWithoutFabricatedActualsAndCanBeWithdrawnAfterAnotherRestore()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var (old,source,active,copy)=await Seed(f);var service=Manual(f);
        var archive=(await service.GetAsync(old.Id))!;Assert.NotNull(archive.Tasks.Single(x=>x.Id==source.Id).Continuation);
        var corrected=await service.RecordRealizationAsync(old.Id,source.Id,new(archive.Version,20,8,true,"Aslında kapsam tamamlandı",WorkedOn:Day),true);
        Assert.Null(corrected.Tasks.Single(x=>x.Id==source.Id).Continuation!.RemainingQuestions);
        var done=await TaskRow(f,copy);Assert.True(done.IsCompleted);Assert.Equal(source.Id,done.CompletionSourceTaskId);Assert.Null(done.ActualMinutes);Assert.Empty(done.WorkDays);
        var exported=await new Coaching.Infrastructure.Repositories.CoachingDataExportRepository(f.Db).ExportStudentDataAsync(source.StudentId,DateTimeOffset.UtcNow,default);
        var exportedTask=exported.StudyPlans!.Single(x=>x.Id==active.Id).Tasks.Single(x=>x.Id==copy);
        Assert.Equal(source.Id,exportedTask.CompletionSourceTaskId);Assert.Null(exportedTask.ActualMinutes);Assert.Equal(done.RealizationHistory.Count,exportedTask.RealizationHistory!.Count);
        Assert.Equal(0,(await Preview(f)).Schedule.Lessons.Single().RequiredMinutes);
        var activeNow=await PlanRow(f,active.Id);var restored=await service.RestoreArchivedAsync(old.Id,new(corrected.Version,active.Id,activeNow.Version));
        // The completion source is now active; withdrawing it must clear inherited archived credit too.
        restored=await service.RecordRealizationAsync(old.Id,source.Id,new(restored.Version,20,8,false,"Tamamlandı işareti hatalıydı",RemainingMinutes:30,RemainingQuestions:10,WorkedOn:Day),true);
        var reopened=await TaskRow(f,copy);Assert.False(reopened.IsCompleted);Assert.Null(reopened.CompletionSourceTaskId);Assert.Null(reopened.ActualMinutes);
        Assert.Equal(30,restored.Tasks.Single(x=>x.Id==source.Id).RemainingMinutes);
        Assert.Contains(reopened.RealizationHistory,x=>x.Action=="ContinuationReconciliation"&&x.PreviouslyCompleted&&!x.Completed);
        Assert.Equal(30,(await Preview(f)).Schedule.Lessons.Single().RequiredMinutes);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task HistoricalScopeCorrectionUpdatesUntouchedContinuationAndInvalidatesItsExistingDraft()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var (old,source,active,copy)=await Seed(f);var service=Manual(f);
        var draft=await service.CreateDraftAsync(new("Önceki önizleme",[new(Day.AddDays(14),"Yeni iş",10,null,false)]));
        var archive=await PlanRow(f,old.Id);var before=await TaskRow(f,copy);
        await service.RecordRealizationAsync(old.Id,source.Id,new(archive.Version,20,8,false,"Kalan kapsam yanlış girilmiş",RemainingMinutes:30,RemainingQuestions:9,WorkedOn:Day),true);
        var updated=await TaskRow(f,copy);Assert.Equal(30,updated.RemainingMinutes);Assert.Equal(9,updated.RemainingQuestions);
        Assert.Equal(Effort(before),Effort(updated));Assert.Equal(before.PredecessorTaskId,updated.PredecessorTaskId);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.PublishAsync(draft.Id,draft.Version));
        Assert.Equal(active.Id,(await f.Db.StudyPlanRevisions.AsNoTracking().SingleAsync(x=>x.IsActive)).Id);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ProgressedContinuationRequiresExplicitCurrentScopeAndRejectsStaleAcknowledgementAtomically()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var (old,source,active,copy)=await Seed(f);var service=Manual(f);
        active=await service.RecordRealizationAsync(active.Id,copy,new(active.Version,10,4,false,RemainingMinutes:15,RemainingQuestions:5,WorkedOn:Day.AddDays(1)),false);
        var archive=(await service.GetAsync(old.Id))!;var continuation=archive.Tasks.Single(x=>x.Id==source.Id).Continuation!;
        Assert.True(continuation.RequiresExplicitScope);Assert.Equal(active.Version,continuation.RevisionVersion);
        var before=JsonSerializer.Serialize(await TaskRow(f,source.Id));var effort=Effort(await TaskRow(f,copy));
        var correction=new StudyRealizationInput(archive.Version,20,8,false,"Kalan kapsam yanlış girilmiş",RemainingMinutes:30,RemainingQuestions:9,WorkedOn:Day);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.RecordRealizationAsync(old.Id,source.Id,correction,true));
        Assert.Equal(before,JsonSerializer.Serialize(await TaskRow(f,source.Id)));Assert.Equal(15,(await TaskRow(f,copy)).RemainingMinutes);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.RecordRealizationAsync(old.Id,source.Id,correction with {ContinuationScope=new(active.Id,active.Version-1,10,3)},true));
        Assert.Equal(before,JsonSerializer.Serialize(await TaskRow(f,source.Id)));
        var result=await service.RecordRealizationAsync(old.Id,source.Id,correction with {ContinuationScope=new(active.Id,active.Version,10,3)},true);
        var updated=await TaskRow(f,copy);Assert.Equal(10,updated.RemainingMinutes);Assert.Equal(3,updated.RemainingQuestions);Assert.Equal(effort,Effort(updated));
        Assert.Equal(active.Version+1,(await PlanRow(f,active.Id)).Version);
        var history=updated.RealizationHistoryJson;
        await service.RecordRealizationAsync(old.Id,source.Id,correction with {ExpectedVersion=result.Version},true);
        Assert.Equal(history,(await TaskRow(f,copy)).RealizationHistoryJson);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task LaterIndependentCompletionSurvivesAncestorScopeAndEffortCorrections()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var (old,source,active,copy)=await Seed(f);var service=Manual(f);
        active=await service.RecordRealizationAsync(active.Id,copy,new(active.Version,22,12,true,WorkedOn:Day.AddDays(1)),false);
        var before=JsonSerializer.Serialize(await TaskRow(f,copy));var archive=await PlanRow(f,old.Id);
        await service.RecordRealizationAsync(old.Id,source.Id,new(archive.Version,18,7,false,"Geçmiş kayıt yanlış girilmiş",RemainingMinutes:45,RemainingQuestions:15,WorkedOn:Day),true);
        Assert.Equal(before,JsonSerializer.Serialize(await TaskRow(f,copy)));Assert.Equal(0,(await Preview(f)).Schedule.Lessons.Single().RequiredMinutes);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task EffortAndDateOnlyCorrectionsDoNotOverwriteLaterScopeOrItsVersion()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var (old,source,active,copy)=await Seed(f);var service=Manual(f);
        active=await service.RecordRealizationAsync(active.Id,copy,new(active.Version,10,4,false,RemainingMinutes:15,RemainingQuestions:5,WorkedOn:Day.AddDays(1)),false);
        var before=JsonSerializer.Serialize(await TaskRow(f,copy));var archive=await PlanRow(f,old.Id);
        var result=await service.RecordRealizationAsync(old.Id,source.Id,new(archive.Version,18,7,false,"Yalnız gerçekleşen miktar hatalı",WorkedOn:Day),true);
        await service.RecordRealizationAsync(old.Id,source.Id,new(result.Version,18,7,false,"Yalnız fiili gün yanlış seçilmiş",WorkedOn:Day.AddDays(2),ReassignFromDate:Day),true);
        Assert.Equal(before,JsonSerializer.Serialize(await TaskRow(f,copy)));Assert.Equal(active.Version,(await PlanRow(f,active.Id)).Version);
        Assert.Equal(Day.AddDays(2),(await TaskRow(f,source.Id)).WorkDays.Single().WorkedOn);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task WithdrawingInheritedCompletionRequiresCurrentScopeAfterFurtherWorkAndPreservesThatWork()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var (old,source,active,copy)=await Seed(f);var service=Manual(f);
        active=await service.RecordRealizationAsync(active.Id,copy,new(active.Version,10,4,false,RemainingMinutes:15,RemainingQuestions:5,WorkedOn:Day.AddDays(1)),false);
        var effort=Effort(await TaskRow(f,copy));var archive=await PlanRow(f,old.Id);
        var closed=await service.RecordRealizationAsync(old.Id,source.Id,new(archive.Version,20,8,true,"Kapsam tamamlandı olarak düzeltildi",WorkedOn:Day),true);
        Assert.True((await TaskRow(f,copy)).IsCompleted);Assert.Equal(effort,Effort(await TaskRow(f,copy)));
        var activeNow=await PlanRow(f,active.Id);var before=JsonSerializer.Serialize(await TaskRow(f,source.Id));
        var correction=new StudyRealizationInput(closed.Version,20,8,false,"Tamamlanma hatalı işaretlenmiş",RemainingMinutes:30,WorkedOn:Day);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.RecordRealizationAsync(old.Id,source.Id,correction,true));
        Assert.Equal(before,JsonSerializer.Serialize(await TaskRow(f,source.Id)));Assert.True((await TaskRow(f,copy)).IsCompleted);
        await service.RecordRealizationAsync(old.Id,source.Id,correction with {ContinuationScope=new(active.Id,activeNow.Version,10,3)},true);
        var reopened=await TaskRow(f,copy);Assert.False(reopened.IsCompleted);Assert.Null(reopened.CompletionSourceTaskId);Assert.Null(reopened.CompletedAt);
        Assert.Equal(10,reopened.RemainingMinutes);Assert.Equal(effort,Effort(reopened));
        Assert.Equal((await TaskRow(f,source.Id)).Version,reopened.RealizationHistory.Last().SourceTaskVersion);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task RestoreConflictRollsBackReconciliationPlanStatesAndDecisionLinkTogether()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var (old,source,active,copy)=await Seed(f);var service=Manual(f);
        active=await service.RecordRealizationAsync(active.Id,copy,new(active.Version,10,4,false,RemainingMinutes:15,WorkedOn:Day.AddDays(1)),false);
        var current=await f.Db.StudyPlanRevisions.SingleAsync(x=>x.Id==active.Id);
        f.Db.Add(StudyPlanTask.Create(current,Day.AddDays(8),"Yeni sabit iş",20,isPinned:true));current.RecordTaskChange();await f.Db.SaveChangesAsync();
        var oldNow=await PlanRow(f,old.Id);var activeNow=await PlanRow(f,active.Id);var before=JsonSerializer.Serialize(await TaskRow(f,source.Id));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.RestoreArchivedAsync(old.Id,new(oldNow.Version,active.Id,activeNow.Version)));
        Assert.Equal(before,JsonSerializer.Serialize(await TaskRow(f,source.Id)));Assert.Equal(StudyPlanStatus.Archived,(await PlanRow(f,old.Id)).Status);
        Assert.True((await PlanRow(f,active.Id)).IsActive);Assert.Equal(copy,(await f.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).StudyTaskId);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ExistingUnreconciledCompletionCanBeReconciledWithoutRewritingItsHistoricalRealizationAndRepeatedAcknowledgementIsIdempotent()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var (old,source,active,copy)=await Seed(f);var service=Manual(f);
        var historical=await f.Db.StudyPlanTasks.SingleAsync(x=>x.Id==source.Id);
        historical.RecordRealization(20,8,true,historical.StudentId,"Önceki sürümde yapılmış düzeltme",workedOn:Day);await f.Db.SaveChangesAsync();
        var before=JsonSerializer.Serialize(await TaskRow(f,source.Id));var archive=await PlanRow(f,old.Id);
        var result=await service.RecordRealizationAsync(old.Id,source.Id,new(archive.Version,20,8,true,"Mevcut tamamlanmayı uzlaştır",WorkedOn:Day),true);
        Assert.Equal(before,JsonSerializer.Serialize(await TaskRow(f,source.Id)));Assert.True((await TaskRow(f,copy)).IsCompleted);
        var activeNow=await PlanRow(f,active.Id);var history=(await TaskRow(f,copy)).RealizationHistoryJson;
        var repeated=await service.RecordRealizationAsync(old.Id,source.Id,new(result.Version,20,8,true,"Mevcut tamamlanmayı uzlaştır",WorkedOn:Day),true);
        Assert.Equal(result.Version,repeated.Version);Assert.Equal(activeNow.Version,(await PlanRow(f,active.Id)).Version);Assert.Equal(history,(await TaskRow(f,copy)).RealizationHistoryJson);
    }

    [Fact]
    public void ReconciliationRejectsAnotherStudentOrWorkRootAndDoesNotCopyEffortOrInventRemainingQuestions()
    {
        var student=Guid.NewGuid();var old=StudyPlanRevision.Create(student,Guid.NewGuid(),1,"Eski");var next=StudyPlanRevision.Create(student,Guid.NewGuid(),1,"Yeni");
        var source=StudyPlanTask.Create(old,Day,"İş",60,plannedQuestions:30);source.RecordRealization(20,8,false,student,remainingMinutes:40,workedOn:Day);
        var copy=StudyPlanTask.Create(next,Day,"Devam",40);copy.CarryFrom(source);copy.ReconcileProgress(source,student,"Güncel kalan kapsamı koru");
        Assert.Equal(40,copy.RemainingMinutes);Assert.Null(copy.RemainingQuestions);Assert.Null(copy.ActualMinutes);Assert.Empty(copy.WorkDays);
        var other=StudyPlanTask.Create(next,Day,"Başka iş",10);
        Assert.Throws<ArgumentException>(()=>copy.ReconcileProgress(other,student,"Başka çalışma engellenmeli"));
        var stranger=StudyPlanTask.Create(StudyPlanRevision.Create(Guid.NewGuid(),Guid.NewGuid(),1,"Başka öğrenci"),Day,"İş",10);
        Assert.Throws<ArgumentException>(()=>copy.ReconcileProgress(stranger,student,"Başka öğrenci engellenmeli"));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task RestoringWithLegacyPartialProgressRequiresDeclaredScopeInsteadOfGuessingFromEffort()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var (old,source,active,copy)=await Seed(f);var service=Manual(f);
        active=await service.RecordRealizationAsync(active.Id,copy,new(active.Version,10,4,false,WorkedOn:Day.AddDays(1)),false);
        var archive=await PlanRow(f,old.Id);var before=JsonSerializer.Serialize(await TaskRow(f,source.Id));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.RestoreArchivedAsync(old.Id,new(archive.Version,active.Id,active.Version)));
        Assert.Equal(before,JsonSerializer.Serialize(await TaskRow(f,source.Id)));Assert.True((await PlanRow(f,active.Id)).IsActive);
        active=await service.SetRemainingScopeAsync(active.Id,copy,new(active.Version,15,5));
        var restored=await service.RestoreArchivedAsync(old.Id,new(archive.Version,active.Id,active.Version));
        Assert.Equal(15,restored.Tasks.Single(x=>x.Id==source.Id).RemainingMinutes);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigrationKeepsAllExistingColumnsAndRejectsLossyDowngradeWithInheritedCompletion()
    {
        const string previous="20261024230308_WeeklyGradeResultRevision";
        await using var pg=await CurriculumPostgresTests.Pg.Create(previous);await pg.Data.Seed(legacyPlanSchema:true);
        async Task<string> Row()=>await pg.Db.Database.SqlQueryRaw<string>("""SELECT (to_jsonb(t)-'PlanKind'-'PlanReason'-'SkippedAt'-'CompletionSourceTaskId')::text AS "Value" FROM coaching.study_plan_tasks t""").SingleAsync();
        var before=await Row();await pg.Db.Database.MigrateAsync();await pg.Db.Database.MigrateAsync();Assert.Equal(before,await Row());Assert.False(pg.Db.Database.HasPendingModelChanges());
        var plan=await pg.Db.StudyPlanRevisions.SingleAsync();var source=await pg.Db.StudyPlanTasks.SingleAsync();
        var copy=StudyPlanTask.Create(plan,Day,"Aynı kapsam",30);copy.RecordRealization(1,null,false,plan.StudentId,remainingMinutes:20,workedOn:Day);
        // Establish the same logical identity before inheriting completion, preserving its own effort.
        pg.Db.Entry(copy).Property(x=>x.OriginalTaskId).CurrentValue=source.Id;
        copy.ReconcileProgress(source,plan.StudentId,"Eski ilerlemeyi koruma testi");pg.Db.Add(copy);await pg.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>pg.Db.GetService<IMigrator>().MigrateAsync(previous));
        // EF Core 10 commits each rolled-back migration on its own, so a refused downgrade can leave newer columns removed;
        // the kept data is therefore read with SQL that does not depend on the current model.
        Assert.True(await pg.Db.Database.SqlQueryRaw<bool>("""SELECT "IsCompleted" AS "Value" FROM coaching.study_plan_tasks WHERE "Id" = {0}""",copy.Id).SingleAsync());
    }
}
