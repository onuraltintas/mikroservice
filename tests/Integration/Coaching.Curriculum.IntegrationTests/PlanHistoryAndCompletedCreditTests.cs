using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class PlanHistoryAndCompletedCreditTests
{
    private static readonly WeeklyOutcomePlanRequest Input = new(new(2027,5,24),new(2027,6,14),0,false);

    [Fact]
    public void ArchivingPublishedPlanRetainsTenPlannedAndFourCompleted_AndDiscardsDrafts()
    {
        var plan=StudyPlanRevision.Create(Guid.NewGuid(),Guid.NewGuid(),1,"Published");plan.Activate();
        var date=new DateOnly(2026,10,19);
        StudyReportTask[] Rows()=>Enumerable.Range(0,10).Select(i=>new StudyReportTask(plan.Status,date,null,30,i<4,i<4?25:null,WasPublished:plan.WasPublished)).ToArray();
        var before=StudyReportCalculator.Calculate(date,date,Rows());plan.Archive();
        var after=StudyReportCalculator.Calculate(date,date,[..Rows(),new(StudyPlanStatus.Archived,date,null,90,false,null)]);
        Assert.Equal(before,after with{Topics=before.Topics});Assert.Equal(10,after.ScheduledTasks);Assert.Equal(40m,after.CompletionPercentage);
        Assert.Equal(300,after.PlannedMinutes);Assert.Equal(100,after.ActualMinutes);
        var emptyPublished=StudyReportCalculator.Calculate(date,date,[new(StudyPlanStatus.Archived,date,null,30,false,null,WasPublished:true)]);
        Assert.Equal(0m,emptyPublished.CompletionPercentage);
    }

    [Fact]
    public void CarriedWorkCountsOnceAndCompletionIsReadFromTheCopy()
    {
        var date=new DateOnly(2026,10,19);var id=Guid.NewGuid();
        var report=StudyReportCalculator.Calculate(date,date,[
            new(StudyPlanStatus.Archived,date,null,30,false,null,WasPublished:true,WorkId:id),
            new(StudyPlanStatus.Active,date,null,30,true,25,WorkId:id)]);
        Assert.Equal(1,report.ScheduledTasks);Assert.Equal(1,report.CompletedTasks);Assert.Equal(30,report.PlannedMinutes);Assert.Equal(25,report.ActualMinutes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedAllocationsReduceNewLoadAcrossActiveAndArchivedPlansWithoutChangingEvidence(bool archived)
    {
        await using var f=new CurriculumManagementTests.Fixture();var service=await WeeklyOutcomePlanTests.Seed(f);
        var old=await f.Db.StudyPlanRevisions.SingleAsync();if(archived){old.Archive();await f.Db.SaveChangesAsync();}
        var before=await Evidence(f);
        var preview=await service.PreviewAsync(Input,default);
        Assert.Equal(30,preview.Schedule.Tasks.Sum(x=>x.Minutes));Assert.Equal(30,Assert.Single(preview.Schedule.Lessons).RequiredMinutes);
        // The original task records 25 actual minutes but covers 30 allocated curriculum minutes.
        var created=await service.CreateAsync(new("Remaining",Input,preview.Fingerprint),default);
        Assert.Equal(30,await f.Db.StudyPlanTasks.Where(x=>x.RevisionId==created.Id).SumAsync(x=>x.PlannedMinutes));
        Assert.Equal(before,await Evidence(f,created.Id));
    }

    [Fact]
    public async Task CompletedAndPendingPinnedCreditsAreCappedAndOtherStudentsDoNotContribute()
    {
        await using var f=new CurriculumManagementTests.Fixture();var service=await WeeklyOutcomePlanTests.Seed(f);
        var old=await f.Db.StudyPlanRevisions.SingleAsync();var topic=await f.Db.StudyCatalogTopics.SingleAsync();var outcome=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();var estimate=await f.Db.Set<OutcomeDurationEstimate>().SingleAsync();
        var pinned=StudyPlanTask.Create(old,Input.StartDate,"Pinned",15,topic.Id,true);pinned.SetWeeklyPlacement(topic.LessonId,"Matematik",1080);
        var foreignPlan=StudyPlanRevision.Create(Guid.NewGuid(),Guid.NewGuid(),1,"Foreign");foreignPlan.Activate();
        var foreign=StudyPlanTask.Create(foreignPlan,Input.StartDate,"Foreign",60,topic.Id);foreign.Complete(600);
        f.Db.AddRange(pinned,foreignPlan,foreign,StudyPlanTaskOutcome.Create(pinned.Id,outcome.Id,estimate.Id,15,"Catalog"),StudyPlanTaskOutcome.Create(foreign.Id,outcome.Id,estimate.Id,60,"Catalog"));await f.Db.SaveChangesAsync();
        var preview=await service.PreviewAsync(Input,default);
        Assert.Equal(15,preview.Schedule.Tasks.Sum(x=>x.Minutes));Assert.Equal(30,Assert.Single(preview.Schedule.Lessons).ScheduledMinutes);
        var created=await service.CreateAsync(new("Pinned remaining",Input,preview.Fingerprint),default);
        var copy=await f.Db.StudyPlanTasks.SingleAsync(x=>x.RevisionId==created.Id&&x.IsPinned);Assert.Equal(pinned.Id,copy.OriginalTaskId);
    }

    [Fact]
    public async Task NewCompletionInvalidatesPreviewAndRepeatedRenewalsNeverCreditTwice()
    {
        await using var f=new CurriculumManagementTests.Fixture();var service=await WeeklyOutcomePlanTests.Seed(f);
        var old=await f.Db.StudyPlanRevisions.SingleAsync();var topic=await f.Db.StudyCatalogTopics.SingleAsync();var outcome=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();var estimate=await f.Db.Set<OutcomeDurationEstimate>().SingleAsync();
        var task=StudyPlanTask.Create(old,new(2026,10,20),"Remaining",30,topic.Id);
        f.Db.AddRange(task,StudyPlanTaskOutcome.Create(task.Id,outcome.Id,estimate.Id,30,"Catalog"));await f.Db.SaveChangesAsync();
        var preview=await service.PreviewAsync(Input,default);Assert.Equal(30,preview.Schedule.Tasks.Sum(x=>x.Minutes));
        task.Complete(300);old.RecordTaskChange();await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>(()=>service.CreateAsync(new("Stale",Input,preview.Fingerprint),default));
        old.Archive();var replacement=StudyPlanRevision.Create(old.StudentId,Guid.NewGuid(),1,"Replacement");replacement.Activate();f.Db.Add(replacement);await f.Db.SaveChangesAsync();
        var next=await service.PreviewAsync(Input,default);Assert.Empty(next.Schedule.Tasks);Assert.Equal(0,Assert.Single(next.Schedule.Lessons).RequiredMinutes);
        Assert.Empty((await service.PreviewAsync(Input,default)).Schedule.Tasks);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task PublishKeepsStudentAndWeeklyReportsStableAndOldRowsUnchanged()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();
        var old=await pg.Db.StudyPlanRevisions.SingleAsync();var task=await pg.Db.StudyPlanTasks.SingleAsync();
        for(var i=0;i<9;i++)pg.Db.Add(StudyPlanTask.Create(old,task.PlannedDate,"Pending "+i,30));await pg.Db.SaveChangesAsync();
        var access=new CoachingAccessPolicy(f.User);var reports=new CoachingStudentStudyReportService(pg.Db,access);
        var reviews=await WeeklyReviewPostgresTests.Setup(pg,old.StudentId,Guid.NewGuid());f.User.UserId=old.StudentId;f.User.Roles=["Student"];
        var before=await reports.GetAsync(task.PlannedDate,task.PlannedDate);var weeklyBefore=(await reviews.GetAsync(null,task.PlannedDate,1,default)).Summary;
        var evidence=await Evidence(f);var plan=StudyPlanRevision.Create(old.StudentId,Guid.NewGuid(),1,"Next");pg.Db.Add(plan);pg.Db.Add(StudyPlanTask.Create(plan,Input.StartDate,"New",30));await pg.Db.SaveChangesAsync();
        var manual=new CoachingManualStudyPlanService(pg.Db,access,new CoachingAutomaticStudyPlanPreviewService(pg.Db,access),new CurriculumManagementTests.NoopPublisher());
        await manual.PublishAsync(plan.Id,plan.Version);pg.Db.ChangeTracker.Clear();
        var after=await reports.GetAsync(task.PlannedDate,task.PlannedDate);Assert.Equal(before.ScheduledTasks,after.ScheduledTasks);Assert.Equal(10,after.ScheduledTasks);Assert.Equal(10m,after.CompletionPercentage);
        Assert.Equal(weeklyBefore,(await reviews.GetAsync(null,task.PlannedDate,1,default)).Summary);Assert.Equal(evidence,await Evidence(f,plan.Id));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigrationPreservesEveryOldColumnAndOnlyBackfillsProvenPublication()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create("20261022221241_OverdueStudySummaries");
        var student=Guid.NewGuid();var active=Guid.NewGuid();var archived=Guid.NewGuid();var unknown=Guid.NewGuid();var referenced=Guid.NewGuid();var draft=Guid.NewGuid();var now=DateTime.UtcNow;
        foreach(var item in new[]{(active,1),(archived,2),(unknown,2),(referenced,2),(draft,0)})
        {
            var planId=Guid.NewGuid();var taskId=Guid.NewGuid();var done=item.Item1==archived;
            await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO coaching.study_plan_revisions ("Id","StudentId","PlanId","RevisionNumber","Title","Status","IsActive","Version","CreatedAt")
                VALUES ({item.Item1},{student},{planId},1,'Historical',{item.Item2},{item.Item2==1},0,{now});
                INSERT INTO coaching.study_plan_tasks ("Id","RevisionId","StudentId","PlannedDate","Title","PlannedMinutes","ActualMinutes","IsPinned","IsCompleted","CompletedAt","Version","CreatedAt")
                VALUES ({taskId},{item.Item1},{student},{new DateOnly(2026,10,19)},'Historical work',30,{(done?(int?)25:null)},false,{done},{(done?(DateTime?)now:null)},0,{now});
                """);
        }
        await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE coaching.study_plan_revisions SET "AutomaticSourceRevisionId"={referenced},"AutomaticSourceRevisionVersion"=0
            WHERE "Id"={active}
            """);
        async Task<string[]> PlanRows()=>await pg.Db.Database.SqlQueryRaw<string>("""SELECT (to_jsonb(p)-'ArchivedAt'-'WasPublished'-'AutomaticOutcomeEvidence')::text AS "Value" FROM coaching.study_plan_revisions p ORDER BY "Id" """).ToArrayAsync();
        async Task<string[]> TaskRows()=>await pg.Db.Database.SqlQueryRaw<string>("""SELECT (to_jsonb(t)-'PlanKind'-'PlanReason'-'SkippedAt'-'OriginalTaskId'-'RealizationHistoryJson'-'RemainingMinutes'-'RemainingQuestions'-'PredecessorTaskId'-'PredecessorVersion'-'ActualWorkJson'-'WorkFromDate'-'WorkToDate'-'CompletionSourceTaskId')::text AS "Value" FROM coaching.study_plan_tasks t ORDER BY "Id" """).ToArrayAsync();
        var plansBefore=await PlanRows();var tasksBefore=await TaskRows();
        await pg.Db.Database.MigrateAsync();await pg.Db.Database.MigrateAsync();
        Assert.Equal(plansBefore,await PlanRows());Assert.Equal(tasksBefore,await TaskRows());
        var plans=await pg.Db.StudyPlanRevisions.AsNoTracking().ToDictionaryAsync(x=>x.Id);
        Assert.True(plans[active].WasPublished);Assert.True(plans[archived].WasPublished);Assert.True(plans[referenced].WasPublished);
        Assert.False(plans[unknown].WasPublished);Assert.False(plans[draft].WasPublished);
        Assert.All(await pg.Db.StudyPlanTasks.ToArrayAsync(),x=>Assert.Null(x.OriginalTaskId));
        Assert.Empty(await pg.Db.Database.GetPendingMigrationsAsync());Assert.False(pg.Db.Database.HasPendingModelChanges());
    }

    private static async Task<string> Evidence(CurriculumManagementTests.Fixture f,Guid? exclude=null)=>JsonSerializer.Serialize(new {
        Tasks=await f.Db.StudyPlanTasks.AsNoTracking().Where(x=>exclude==null||x.RevisionId!=exclude).OrderBy(x=>x.Id).ToArrayAsync(),
        Links=await f.Db.Set<StudyPlanTaskOutcome>().AsNoTracking().Where(x=>!f.Db.StudyPlanTasks.Any(t=>t.Id==x.TaskId&&t.RevisionId==exclude)).OrderBy(x=>x.TaskId).ThenBy(x=>x.OutcomeId).ToArrayAsync() });
}
