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
    private static async Task<CoachingWeeklyOutcomePlanService> Seed(CurriculumManagementTests.Fixture f)
    {
        await f.Seed();f.User.Roles=["Student"];f.User.UserId=(await f.Db.StudyPlanTasks.SingleAsync()).StudentId;
        var lesson=await f.Db.StudyCatalogLessons.SingleAsync();var weekly=WeeklyStudyDraft.Create(f.User.UserId.Value);
        weekly.Replace("Europe/Istanbul",[new(Guid.NewGuid(),lesson.Id,lesson.Name,"LGS",DayOfWeek.Monday,1080,30)]);
        f.Db.Add(weekly);f.Db.Add(StudentLearningContext.Create(f.User.UserId.Value,2026,LearningPurpose.Lgs,2027,null,null));await f.Db.SaveChangesAsync();
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
}
