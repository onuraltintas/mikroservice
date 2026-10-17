using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class WeeklyOutcomePlanTests
{
    private static readonly WeeklyOutcomePlanRequest Input=new(new(2027,5,24),new(2027,6,1),0,false);
    internal static async Task<CoachingWeeklyOutcomePlanService> Seed(CurriculumManagementTests.Fixture f)
    {
        await f.Seed();f.User.Roles=["Student"];f.User.UserId=(await f.Db.StudyPlanTasks.SingleAsync()).StudentId;
        var lesson=await f.Db.StudyCatalogLessons.SingleAsync();var weekly=WeeklyStudyDraft.Create(f.User.UserId.Value);
        weekly.Replace("Europe/Istanbul",[new(Guid.NewGuid(),lesson.Id,lesson.Name,"LGS",DayOfWeek.Monday,1080,30)]);
        f.Db.Add(weekly);f.Db.Add(Coaching.Domain.Profiles.CoachingStudentProfile.Create(f.User.UserId.Value));f.Db.Add(StudentLearningContext.Create(f.User.UserId.Value,2026,LearningPurpose.Lgs,2027,null,null));await f.Db.SaveChangesAsync();
        return new(f.Db,new CoachingAccessPolicy(f.User),new WeeklyStudyDraftTests.Catalog(f.Db),TimeProvider.System);
    }
    [Fact]
    public async Task PreviewIsReadOnly_AndConfirmationCreatesTimedDraftWithoutChangingOldWork()
    {
        await using var f=new CurriculumManagementTests.Fixture();var service=await Seed(f);
        var before=JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync());var preview=await service.PreviewAsync(Input,default);
        Assert.True(preview.CanCreate);Assert.Equal(2,preview.Schedule.Tasks.Count);Assert.Equal(new DateOnly(2027,5,31),preview.Schedule.Lessons[0].FinishDate);
        Assert.Single(await f.Db.StudyPlanRevisions.ToArrayAsync());
        var created=await service.CreateAsync(new("Yıllık plan",Input,preview.Fingerprint),default);
        var revision=await f.Db.StudyPlanRevisions.SingleAsync(x=>x.Id==created.Id);Assert.Equal(StudyPlanStatus.Draft,revision.Status);Assert.NotNull(revision.WeeklySource);
        var tasks=await f.Db.StudyPlanTasks.Where(x=>x.RevisionId==created.Id).ToArrayAsync();Assert.All(tasks,x=>Assert.Equal(1080,x.PlannedStartMinute));
        Assert.Equal(60,tasks.Sum(x=>x.PlannedMinutes));Assert.Equal(2,await f.Db.Set<StudyPlanTaskOutcome>().CountAsync(x=>tasks.Select(t=>t.Id).Contains(x.TaskId)));
        Assert.Equal(before,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().Where(x=>x.RevisionId!=created.Id).ToArrayAsync()));
        Assert.Single(await f.Db.StudyPlanRevisions.Where(x=>x.IsActive).ToArrayAsync());
        var export=await new Coaching.Infrastructure.Repositories.CoachingDataExportRepository(f.Db).ExportStudentDataAsync(f.User.UserId!.Value,DateTimeOffset.UtcNow,default);
        var exported=export.StudyPlans!.Single(x=>x.Id==created.Id);Assert.NotNull(exported.WeeklySource);Assert.All(exported.Tasks,x=>Assert.Equal(1080,x.PlannedStartMinute));
    }
    [Fact]
    public async Task ChangedWeeklyDraftOrEstimateRejectsOldPreview_AndIncompleteCurriculumBlocksSaving()
    {
        await using var f=new CurriculumManagementTests.Fixture();var service=await Seed(f);var preview=await service.PreviewAsync(Input,default);
        var estimate=await f.Db.Set<OutcomeDurationEstimate>().SingleAsync();estimate.Withdraw();await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.CreateAsync(new("Yıllık plan",Input,preview.Fingerprint),default));
        var missing=await service.PreviewAsync(Input,default);Assert.False(missing.CanCreate);Assert.NotEmpty(missing.Issues);
        Assert.Single(await f.Db.StudyPlanRevisions.ToArrayAsync());
        var weekly=await f.Db.Set<WeeklyStudyDraft>().SingleAsync();weekly.Replace(weekly.TimeZoneId,[]);await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.PreviewAsync(Input,default));
    }
    [Fact]
    public async Task CapacityShortfallRequiresChangedDatesOrHours_NotSilentPartialPlan()
    {
        await using var f=new CurriculumManagementTests.Fixture();var service=await Seed(f);var shortRequest=Input with {EndDate=Input.StartDate};
        var preview=await service.PreviewAsync(shortRequest,default);Assert.False(preview.CanCreate);Assert.Equal(30,preview.Schedule.Lessons[0].RemainingMinutes);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.CreateAsync(new("Eksik plan",shortRequest,preview.Fingerprint),default));
        var extended=await service.PreviewAsync(shortRequest with {EndDate=preview.Schedule.RecommendedEndDate!.Value},default);Assert.True(extended.CanCreate);
        f.User.Roles=["Teacher"];await Assert.ThrowsAsync<BusinessRuleException>(()=>service.PreviewAsync(Input,default));
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ChangedCalendarCannotBePublished_AndWeeklyPlanCannotBeFlattenedByManualReplacement()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var service=await Seed(f);var preview=await service.PreviewAsync(Input,default);
        var created=await service.CreateAsync(new("Yıllık plan",Input,preview.Fingerprint),default);
        var access=new CoachingAccessPolicy(f.User);var manual=new CoachingManualStudyPlanService(f.Db,access,new CoachingAutomaticStudyPlanPreviewService(f.Db,access),new CurriculumManagementTests.NoopPublisher());
        var read=(await manual.GetAsync(created.Id))!;Assert.Equal(1080,read.Tasks[0].PlannedStartMinute);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>manual.ReplaceDraftAsync(created.Id,created.Version,new("Elle değiştir",[new(Input.StartDate,"Yeni",30,null,false)])));
        var weekly=await f.Db.Set<WeeklyStudyDraft>().SingleAsync();weekly.Replace(weekly.TimeZoneId,weekly.Slots.Select(x=>x with {DurationMinutes=60}).ToArray());await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(()=>manual.PublishAsync(created.Id,created.Version));
        Assert.NotEqual(created.Id,(await f.Db.StudyPlanRevisions.SingleAsync(x=>x.IsActive)).Id);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task PublishPreservesCompletedHistory_AndPinnedWorkReservesItsExactTime()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var service=await Seed(f);
        var old=await f.Db.StudyPlanRevisions.SingleAsync(x=>x.IsActive);var topic=await f.Db.StudyCatalogTopics.SingleAsync();
        var pinned=StudyPlanTask.Create(old,Input.StartDate,"Sabit görev",15,topic.Id,true);pinned.SetWeeklyPlacement(topic.LessonId,"Matematik",1080);f.Db.Add(pinned);await f.Db.SaveChangesAsync();
        var before=JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().Where(x=>x.IsCompleted).ToArrayAsync());
        var preview=await service.PreviewAsync(Input with {EndDate=new(2027,6,7)},default);Assert.True(preview.CanCreate);Assert.Equal(1095,preview.Schedule.Tasks[0].StartMinute);Assert.Equal(1,preview.ProtectedTaskCount);
        var created=await service.CreateAsync(new("Korumalı plan",preview.Request,preview.Fingerprint),default);var access=new CoachingAccessPolicy(f.User);
        var manual=new CoachingManualStudyPlanService(f.Db,access,new CoachingAutomaticStudyPlanPreviewService(f.Db,access),new CurriculumManagementTests.NoopPublisher());
        var published=await manual.PublishAsync(created.Id,created.Version);Assert.Equal(StudyPlanStatus.Active,published.Status);Assert.NotNull(published.WeeklySource);
        Assert.Equal(before,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().Where(x=>x.IsCompleted).ToArrayAsync()));
        var copied=Assert.Single(published.Tasks.Where(x=>x.IsPinned));Assert.False(copied.IsCompleted);Assert.Equal(1080,copied.PlannedStartMinute);
        Assert.Equal(StudyPlanStatus.Archived,(await f.Db.StudyPlanRevisions.AsNoTracking().SingleAsync(x=>x.Id==old.Id)).Status);
    }
    [Fact]
    public async Task PinnedOutcomeMinutesAreNotPlannedTwice()
    {
        await using var f=new CurriculumManagementTests.Fixture();var service=await Seed(f);var old=await f.Db.StudyPlanRevisions.SingleAsync(x=>x.IsActive);
        var outcome=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();var estimate=await f.Db.Set<OutcomeDurationEstimate>().SingleAsync();var topic=await f.Db.StudyCatalogTopics.SingleAsync();
        var pinned=StudyPlanTask.Create(old,Input.StartDate,"Sabit kazanım",15,topic.Id,true);pinned.SetWeeklyPlacement(topic.LessonId,"Matematik",1080);f.Db.Add(pinned);f.Db.Add(StudyPlanTaskOutcome.Create(pinned.Id,outcome.Id,estimate.Id,15,"Catalog"));await f.Db.SaveChangesAsync();
        var preview=await service.PreviewAsync(Input,default);Assert.True(preview.CanCreate);Assert.Equal(45,preview.Schedule.Tasks.Sum(x=>x.Minutes));Assert.Equal(60,preview.Schedule.Lessons[0].ScheduledMinutes);
        var created=await service.CreateAsync(new("Tekrarsız plan",Input,preview.Fingerprint),default);var ids=await f.Db.StudyPlanTasks.Where(x=>x.RevisionId==created.Id).Select(x=>x.Id).ToArrayAsync();
        Assert.Equal(60,await f.Db.Set<StudyPlanTaskOutcome>().Where(x=>Enumerable.Contains(ids,x.TaskId)).SumAsync(x=>x.AllocatedMinutes));
    }
}
