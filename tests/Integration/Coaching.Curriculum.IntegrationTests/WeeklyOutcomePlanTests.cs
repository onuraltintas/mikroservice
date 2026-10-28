using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class WeeklyOutcomePlanTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task FreedLessonHoursAreReassignedOnlyWhenChosen_AndSavedWithExactOutcomeLinks()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var service=await Seed(f);
        var math=await f.Db.StudyCatalogLessons.SingleAsync();var mathOutcome=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();
        var mathEstimate=OutcomeDurationEstimate.Create(mathOutcome.Id,2,40,40,40,"ReviewedStudy","Medium","Aktarım test süresi",f.User.UserId);mathEstimate.Publish();
        var lesson=StudyCatalogLesson.Create("test","turkish-reuse","Türkçe",8,"LGS");lesson.SetActive(true);
        var unit=StudyCatalogUnit.Create("test","turkish-unit",lesson.Id,"Türkçe",1);unit.SetActive(true);
        var topic=StudyCatalogTopic.Create("test","turkish-topic",lesson.Id,unit.Id,"Test konusu",null,1);topic.SetActive(true);
        var edition=OfficialCurriculumEdition.Create("test-reuse-2026","LGS",8,"Türkçe",2026,2026,"https://meb.gov.tr/test",new string('b',64),"https://meb.gov.tr/test");
        var official=OfficialCurriculumTopic.Create(edition.Id,"T.TEST","Test konusu",1,1);
        var outcome=StudyCatalogOutcome.Create(edition.Id,official.Id,"T.TEST.1","Yerel aktarım testi","",1,1);
        var estimate=OutcomeDurationEstimate.Create(outcome.Id,1,10,10,10,"ReviewedStudy","Medium","Aktarım test süresi",f.User.UserId);estimate.Publish();
        var mapping=StudyCatalogTopicOutcome.Create(topic.Id,outcome.Id,"Test eşleştirmesi");mapping.Review(true,"Test eşleştirmesi",f.User.UserId!.Value);
        f.Db.AddRange(mathEstimate,lesson,unit,topic,edition,official,outcome,estimate,mapping);
        var weekly=await f.Db.Set<WeeklyStudyDraft>().SingleAsync();weekly.Replace(weekly.TimeZoneId,[..weekly.Slots,new(Guid.NewGuid(),lesson.Id,lesson.Name,"LGS",DayOfWeek.Tuesday,1080,30)]);await f.Db.SaveChangesAsync();
        var input=Input with {ExpectedWeeklyDraftVersion=weekly.Version};var normal=await service.PreviewAsync(input,default);
        Assert.False(normal.CanCreate);Assert.Equal(30,normal.Schedule.Lessons.Single(x=>x.LessonId==math.Id).RemainingMinutes);
        var reused=await service.PreviewAsync(input with {ReuseFreedSlots=true},default);Assert.True(reused.CanCreate);
        var transferred=Assert.Single(reused.Schedule.Tasks.Where(x=>x.ReusedSlot));Assert.Equal(math.Id,transferred.LessonId);Assert.Equal(new DateOnly(2027,6,1),transferred.Date);Assert.Equal(1080,transferred.StartMinute);
        var created=await service.CreateAsync(new("Aktarım testi",reused.Request,reused.Fingerprint),default);
        Assert.True((await f.Db.StudyPlanRevisions.SingleAsync(x=>x.Id==created.Id)).WeeklySource!.ReuseFreedSlots);
        var taskIds=await f.Db.StudyPlanTasks.Where(x=>x.RevisionId==created.Id).Select(x=>x.Id).ToArrayAsync();
        Assert.Equal(120,await f.Db.Set<StudyPlanTaskOutcome>().Where(x=>Enumerable.Contains(taskIds,x.TaskId)).SumAsync(x=>x.AllocatedMinutes));
        Assert.Equal(25,(await f.Db.StudyPlanTasks.SingleAsync(x=>x.IsCompleted)).ActualMinutes);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TopicSequenceRespectsParentOrderBeforeChildOrder()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var service=await Seed(f);
        var old=await f.Db.StudyCatalogTopics.SingleAsync();
        var early=StudyCatalogTopic.Create("test","early-parent",old.LessonId,old.UnitId,"İlk üst konu",null,1);early.SetActive(true);
        var late=StudyCatalogTopic.Create("test","late-parent",old.LessonId,old.UnitId,"İkinci üst konu",null,2);late.SetActive(true);
        var child=StudyCatalogTopic.Create("test","early-child",old.LessonId,old.UnitId,"İlk konunun ikinci alt konusu",early.Id,2);child.SetActive(true);
        f.Db.Entry(old).Property(x=>x.ParentId).CurrentValue=late.Id;
        var existing=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();
        var outcome=StudyCatalogOutcome.Create(existing.EditionId,existing.OfficialTopicId,"M.TEST.2","Sıra doğrulama","",73,2);
        var estimate=OutcomeDurationEstimate.Create(outcome.Id,1,10,10,10,"ReviewedStudy","Medium","Test dayanağı",f.User.UserId);estimate.Publish();
        var mapping=StudyCatalogTopicOutcome.Create(child.Id,outcome.Id,"Sıra testi");mapping.Review(true,"Sıra testi",f.User.UserId!.Value);
        f.Db.AddRange(early,late,child,outcome,estimate,mapping);await f.Db.SaveChangesAsync();
        var preview=await service.PreviewAsync(Input with {EndDate=new(2027,6,14)},default);
        Assert.True(preview.CanCreate);Assert.Equal(child.Id,preview.Schedule.Tasks[0].Outcome.TopicId);
        Assert.Equal(old.Id,preview.Schedule.Tasks[1].Outcome.TopicId);
        early.Edit(early.Name,3,null);await f.Db.SaveChangesAsync();
        var reordered=await service.PreviewAsync(Input with {EndDate=new(2027,6,14)},default);
        Assert.Equal(old.Id,reordered.Schedule.Tasks[0].Outcome.TopicId);
    }
    [Fact]
    public async Task ApiRequiresStudentAndReturnsOptionsValidationConflictAndOwnedNotFound()
    {
        await using var f=new CurriculumManagementTests.Fixture();var service=await Seed(f);var api=new Coaching.API.Controllers.WeeklyOutcomePlansController(service);
        var options=await service.OptionsAsync(default);Assert.Equal(WeeklyOutcomeScheduler.DefaultEnd(options.StartDate),options.EndDate);
        Assert.Equal("Student",Assert.Single(typeof(Coaching.API.Controllers.WeeklyOutcomePlansController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute),true).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()).Roles);
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(await api.Options(default));Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(await api.Preview(Input,default));
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(await api.Preview(Input with {EndDate=Input.StartDate.AddDays(-1)},default));
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(await api.Create(new("Plan",Input,"bad"),default));
        var p=await service.PreviewAsync(Input,default);Assert.IsType<Microsoft.AspNetCore.Mvc.CreatedAtActionResult>(await api.Create(new("Plan",Input,p.Fingerprint),default));
        Assert.IsType<Microsoft.AspNetCore.Mvc.ConflictObjectResult>(await api.Create(new("Plan",Input,p.Fingerprint),default));
        f.User.Roles=["Teacher"];Assert.Equal(403,Assert.IsType<Microsoft.AspNetCore.Mvc.ObjectResult>(await api.Options(default)).StatusCode);
        f.User.Roles=["Student"];f.User.UserId=Guid.NewGuid();Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundObjectResult>(await api.Options(default));
    }
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
        Assert.True(preview.CanCreate);Assert.Equal(1,preview.Schedule.Tasks.Count);Assert.Equal(new DateOnly(2027,5,24),preview.Schedule.Lessons[0].FinishDate);
        Assert.Single(await f.Db.StudyPlanRevisions.ToArrayAsync());
        var created=await service.CreateAsync(new("Yıllık plan",Input,preview.Fingerprint),default);
        var revision=await f.Db.StudyPlanRevisions.SingleAsync(x=>x.Id==created.Id);Assert.Equal(StudyPlanStatus.Draft,revision.Status);Assert.NotNull(revision.WeeklySource);
        var tasks=await f.Db.StudyPlanTasks.Where(x=>x.RevisionId==created.Id).ToArrayAsync();Assert.All(tasks,x=>Assert.Equal(1080,x.PlannedStartMinute));
        Assert.Equal(30,tasks.Sum(x=>x.PlannedMinutes));Assert.Equal(1,await f.Db.Set<StudyPlanTaskOutcome>().CountAsync(x=>tasks.Select(t=>t.Id).Contains(x.TaskId)));
        Assert.Equal(before,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().Where(x=>x.RevisionId!=created.Id).ToArrayAsync()));
        Assert.Single(await f.Db.StudyPlanRevisions.Where(x=>x.IsActive).ToArrayAsync());
        var export=await new Coaching.Infrastructure.Repositories.CoachingDataExportRepository(f.Db).ExportStudentDataAsync(f.User.UserId!.Value,DateTimeOffset.UtcNow,default);
        var exported=export.StudyPlans!.Single(x=>x.Id==created.Id);Assert.NotNull(exported.WeeklySource);Assert.All(exported.Tasks,x=>Assert.Equal(1080,x.PlannedStartMinute));
    }
    [Fact]
    public async Task CompletedTopicsAreExcludedFromNewPreviewButNeedsReviewTopicsRemain()
    {
        await using var f=new CurriculumManagementTests.Fixture();var service=await Seed(f);
        var existingTasks=JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync());
        var completedTopic=await f.Db.StudyCatalogTopics.SingleAsync();var existing=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();
        var pendingTopic=StudyCatalogTopic.Create("test","pending-topic",completedTopic.LessonId,completedTopic.UnitId,"Tekrar konusu",null,2);pendingTopic.SetActive(true);
        var pendingOutcome=StudyCatalogOutcome.Create(existing.EditionId,existing.OfficialTopicId,"M.TEST.PENDING","Tekrar kazanımı","",73,3);
        var estimate=OutcomeDurationEstimate.Create(pendingOutcome.Id,1,15,10,5,"ReviewedStudy","Medium","Test doğrulanmış çalışma süresi",f.User.UserId);estimate.Publish();
        var mapping=StudyCatalogTopicOutcome.Create(pendingTopic.Id,pendingOutcome.Id,"Tekrar konusu eşleştirmesi");mapping.Review(true,"Test eşleştirmesi",f.User.UserId!.Value);
        f.Db.AddRange(pendingTopic,pendingOutcome,estimate,mapping);var context=await f.Db.StudentLearningContexts.SingleAsync();
        context.ReplaceSelections(context.CoursePreferences,
        [StudentTopicState.Create(context.Id,completedTopic.Id,DeclaredTopicStatus.Studied,f.User.UserId.Value),
         StudentTopicState.Create(context.Id,pendingTopic.Id,DeclaredTopicStatus.NeedsReview,f.User.UserId.Value)],true);
        await f.Db.SaveChangesAsync();var preview=await service.PreviewAsync(Input,default);
        Assert.True(preview.CanCreate);Assert.All(preview.Schedule.Tasks,x=>Assert.Equal(pendingTopic.Id,x.Outcome.TopicId));
        Assert.DoesNotContain(preview.Schedule.Tasks,x=>x.Outcome.OutcomeId==existing.Id);
        Assert.Single(await f.Db.StudyPlanRevisions.Where(x=>x.IsActive).ToArrayAsync());
        Assert.Equal(existingTasks,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync()));
        context.ReplaceSelections(context.CoursePreferences,
        [StudentTopicState.Create(context.Id,completedTopic.Id,DeclaredTopicStatus.Studied,f.User.UserId.Value),
         StudentTopicState.Create(context.Id,pendingTopic.Id,DeclaredTopicStatus.Studied,f.User.UserId.Value)],true);
        await f.Db.SaveChangesAsync();var allDone=await service.PreviewAsync(Input,default);
        Assert.False(allDone.CanCreate);Assert.Empty(allDone.Issues);
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
        await using var f=new CurriculumManagementTests.Fixture();var service=await Seed(f);var outcome=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();var estimate=OutcomeDurationEstimate.Create(outcome.Id,2,30,30,30,"ReviewedStudy","Medium","Kapasite testi",f.User.UserId);estimate.Publish();f.Db.Add(estimate);await f.Db.SaveChangesAsync();var shortRequest=Input with {EndDate=Input.StartDate};
        var preview=await service.PreviewAsync(shortRequest,default);Assert.False(preview.CanCreate);Assert.Equal(30,preview.Schedule.Lessons[0].RemainingMinutes);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.CreateAsync(new("Eksik plan",shortRequest,preview.Fingerprint),default));
        var extended=await service.PreviewAsync(shortRequest with {EndDate=preview.Schedule.RecommendedEndDate!.Value},default);Assert.True(extended.CanCreate);
        f.User.Roles=["Teacher"];await Assert.ThrowsAsync<BusinessRuleException>(()=>service.PreviewAsync(Input,default));
    }
    // Roadmap D1: creating a plan shares the catalog lock with other plans (only catalog changes take it exclusively),
    // so one student's plan no longer waits for every other student's plan.
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task PlansOfDifferentStudentsDoNotQueueBehindEachOther()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var service=await Seed(f);var preview=await service.PreviewAsync(Input,default);
        await using var other=new CoachingDbContext(pg.Options);await other.Database.OpenConnectionAsync();
        await other.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock_shared(4188, 100)");
        try
        {
            var creating=service.CreateAsync(new("Eşzamanlı plan",Input,preview.Fingerprint),default);
            Assert.Same(creating,await Task.WhenAny(creating,Task.Delay(TimeSpan.FromSeconds(20))));
            Assert.True((await creating).Version>0);
        }
        finally { await other.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock_all()"); }
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
        var later=published.Tasks.First(x=>!x.IsPinned&&x.PlannedDate>Input.StartDate);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>manual.RescheduleTaskAsync(created.Id,later.Id,published.Version,Input.StartDate));
    }
    [Fact]
    public async Task PinnedOutcomeMinutesAreNotPlannedTwice()
    {
        await using var f=new CurriculumManagementTests.Fixture();var service=await Seed(f);var old=await f.Db.StudyPlanRevisions.SingleAsync(x=>x.IsActive);
        var outcome=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();var estimate=await f.Db.Set<OutcomeDurationEstimate>().SingleAsync();var topic=await f.Db.StudyCatalogTopics.SingleAsync();
        var pinned=StudyPlanTask.Create(old,Input.StartDate,"Sabit kazanım",15,topic.Id,true);pinned.SetWeeklyPlacement(topic.LessonId,"Matematik",1080);f.Db.Add(pinned);f.Db.Add(StudyPlanTaskOutcome.Create(pinned.Id,outcome.Id,estimate.Id,15,"Catalog"));await f.Db.SaveChangesAsync();
        var preview=await service.PreviewAsync(Input,default);Assert.True(preview.CanCreate);Assert.Equal(15,preview.Schedule.Tasks.Sum(x=>x.Minutes));Assert.Equal(30,preview.Schedule.Lessons[0].ScheduledMinutes);
        var created=await service.CreateAsync(new("Tekrarsız plan",Input,preview.Fingerprint),default);var ids=await f.Db.StudyPlanTasks.Where(x=>x.RevisionId==created.Id).Select(x=>x.Id).ToArrayAsync();
        Assert.Equal(30,await f.Db.Set<StudyPlanTaskOutcome>().Where(x=>Enumerable.Contains(ids,x.TaskId)).SumAsync(x=>x.AllocatedMinutes));
    }
    [Fact]
    public async Task SavedPlanKeepsItsTimezoneEvenIfTheWeeklyDraftLaterChanges()
    {
        await using var f=new CurriculumManagementTests.Fixture();var service=await Seed(f);var weekly=await f.Db.Set<WeeklyStudyDraft>().SingleAsync();weekly.Replace("UTC",weekly.Slots);await f.Db.SaveChangesAsync();
        var input=Input with {ExpectedWeeklyDraftVersion=weekly.Version};var preview=await service.PreviewAsync(input,default);var created=await service.CreateAsync(new("Saat dilimi",input,preview.Fingerprint),default);
        Assert.Equal("UTC",(await f.Db.StudyPlanRevisions.SingleAsync(x=>x.Id==created.Id)).WeeklySource!.TimeZoneId);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task DatabaseRejectsPartialWeeklyPlacement()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();await pg.Data.Seed();var task=await pg.Db.StudyPlanTasks.SingleAsync();var lesson=Guid.NewGuid();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>pg.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE coaching.study_plan_tasks SET \"WeeklyLessonId\"={lesson},\"LessonNameSnapshot\"='Matematik' WHERE \"Id\"={task.Id}"));
    }
}
