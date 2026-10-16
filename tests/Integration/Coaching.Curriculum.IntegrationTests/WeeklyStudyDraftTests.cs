using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Coaching.Infrastructure.Repositories;
using Coaching.Infrastructure.Privacy;
using EduPlatform.Shared.Contracts.Events.Privacy;
using Coaching.Application.Attachments;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class WeeklyStudyDraftTests
{
    [Fact]
    public async Task WeeklyDraftIsExportedAndIncludedInOwnedAccountErasure()
    {
        await using var f=new CurriculumManagementTests.Fixture();await f.Seed();f.User.Roles=["Student"];
        var student=f.User.UserId!.Value;var lesson=(await f.Db.StudyCatalogLessons.SingleAsync()).Id;
        await Service(f,new(f.Db)).SaveAsync(new(null,"Europe/Istanbul",[Slot(lesson,1080,60)]),default);
        var other=WeeklyStudyDraft.Create(Guid.NewGuid());other.Replace("Europe/Istanbul",[]);f.Db.Add(other);await f.Db.SaveChangesAsync();
        var export=await new CoachingDataExportRepository(f.Db).ExportStudentDataAsync(student,DateTimeOffset.UtcNow,default);
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(export));Assert.Single(json.RootElement.GetProperty("WeeklyDraft").GetProperty("Slots").EnumerateArray());
        var id=Guid.NewGuid();var eventId=Guid.NewGuid();var now=DateTime.UtcNow;
        var assessment=await new CoachingErasureAssessmentService(f.Db,TimeProvider.System).AssessAsync(new(eventId,id,student,now,true,PersonalDataScope.Coaching),default);
        Assert.Equal(1,assessment.StudyPlanningRecordCount);
        await new CoachingErasureExecutionService(f.Db,new NoAttachments(),TimeProvider.System).ExecuteAsync(new(eventId,id,student,now,PersonalDataScope.Coaching),default);
        Assert.False(await f.Db.Set<WeeklyStudyDraft>().AnyAsync(x=>x.StudentId==student));Assert.True(await f.Db.Set<WeeklyStudyDraft>().AnyAsync(x=>x.Id==other.Id));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigrationRoundTripAndConcurrentEditsPreserveExistingWork()
    {
        await using var f=await CurriculumPostgresTests.Pg.Create();await f.Data.Seed();f.Data.User.Roles=["Student"];
        var before=JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync());
        await f.Db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync("20261016204727_ExamScheduledDate");
        await f.Db.Database.MigrateAsync();await f.Db.Database.MigrateAsync();
        var lesson=(await f.Db.StudyCatalogLessons.SingleAsync()).Id;var svc=Service(f.Data,new(f.Db));
        var draft=await svc.SaveAsync(new(null,"Europe/Istanbul",[Slot(lesson,1080,30),Slot(lesson,1110,30)]),default);
        await using(var second=new Coaching.Infrastructure.Data.CoachingDbContext(f.Options))
        {
            var writer=new CoachingWeeklyStudyDraftService(second,new CoachingAccessPolicy(f.Data.User),new Catalog(second));
            await writer.SaveAsync(new(draft.Version,"Europe/Istanbul",[Slot(lesson,1200,60)]),default);
        }
        await Assert.ThrowsAsync<BusinessRuleException>(()=>svc.SaveAsync(new(draft.Version,"Europe/Istanbul",[]),default));
        var restored=(await svc.GetAsync(default)).Draft!;Assert.Equal(1200,Assert.Single(restored.Slots).StartMinute);
        Assert.Equal(before,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync()));
    }
    [Fact]
    public async Task ReadDoesNotCreateDraft_SaveReopensHalfHourLessonsAndPreservesPlansAndHours()
    {
        await using var f=new CurriculumManagementTests.Fixture();await f.Seed();
        var old=await f.Db.StudyPlanTasks.SingleAsync();f.User.UserId=old.StudentId;f.User.Roles=["Student"];
        var before=JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync());
        var hours=StudyAvailability.Create(old.StudentId,"Europe/Istanbul");hours.ReplaceWindows([new(DayOfWeek.Monday,1080,1200)]);f.Db.Add(hours);await f.Db.SaveChangesAsync();
        var hoursBefore=JsonSerializer.Serialize(hours);var setup=new Catalog(f.Db);var service=Service(f,setup);
        Assert.Null((await service.GetAsync(default)).Draft);Assert.Empty(await f.Db.Set<WeeklyStudyDraft>().ToArrayAsync());
        var lesson=(await f.Db.StudyCatalogLessons.SingleAsync()).Id;
        var saved=await service.SaveAsync(new(null,"Europe/Istanbul",[Slot(lesson,1080,30),Slot(lesson,1110,30)]),default);
        Assert.Equal(2,saved.Slots.Count);Assert.Equal(JsonSerializer.Serialize(saved),JsonSerializer.Serialize((await service.GetAsync(default)).Draft));
        var s=saved.Slots[0];
        var edited=await service.SaveAsync(new(saved.Version,"Europe/Istanbul",[new(s.Id,s.LessonId,s.Day,1080,60)]),default);
        Assert.Single(edited.Slots);Assert.Equal(60,edited.Slots[0].DurationMinutes);
        Assert.Equal(before,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync()));
        Assert.Equal(hoursBefore,JsonSerializer.Serialize(await f.Db.StudyAvailability.SingleAsync()));
        Assert.True((await f.Db.StudyPlanRevisions.SingleAsync()).IsActive);
    }

    [Theory]
    [InlineData(1080,45)] [InlineData(1085,30)] [InlineData(1410,60)] [InlineData(-30,30)] [InlineData(1080,0)]
    public async Task InvalidTimeOrDurationIsRejected(int start,int duration)
    {
        await using var f=new CurriculumManagementTests.Fixture();await f.Seed();f.User.Roles=["Student"];
        var lesson=(await f.Db.StudyCatalogLessons.SingleAsync()).Id;
        await Assert.ThrowsAsync<ArgumentException>(()=>Service(f,new(f.Db)).SaveAsync(new(null,"Europe/Istanbul",[Slot(lesson,start,duration)]),default));
        Assert.Empty(await f.Db.Set<WeeklyStudyDraft>().ToArrayAsync());
    }

    [Fact]
    public async Task OverlapDuplicateIdInvalidDayAndOutOfScopeLessonAreRejected_AndMidnightEndIsAllowed()
    {
        await using var f=new CurriculumManagementTests.Fixture();await f.Seed();f.User.Roles=["Student"];
        var lesson=(await f.Db.StudyCatalogLessons.SingleAsync()).Id;var svc=Service(f,new(f.Db));var a=Slot(lesson,1080,60);
        foreach(var slots in new IReadOnlyList<WeeklySlotInput>[] { [a,Slot(lesson,1110,30)],[a,a],[a with {Day=(DayOfWeek)9}],[a with {LessonId=Guid.NewGuid()}] })
            await Assert.ThrowsAsync<ArgumentException>(()=>svc.SaveAsync(new(null,"Europe/Istanbul",slots),default));
        var saved=await svc.SaveAsync(new(null,"Europe/Istanbul",[Slot(lesson,1410,30)]),default);Assert.Single(saved.Slots);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>svc.SaveAsync(new(null,"Europe/Istanbul",[]),default));
        await svc.SaveAsync(new(saved.Version,"Europe/Istanbul",[]),default);Assert.Empty((await svc.GetAsync(default)).Draft!.Slots);
    }

    [Fact]
    public async Task DraftIsStudentOwned_StaleWritesFail_AndUnavailableLessonKeepsItsSavedLabel()
    {
        await using var f=new CurriculumManagementTests.Fixture();await f.Seed();f.User.Roles=["Student"];
        var setup=new Catalog(f.Db);var svc=Service(f,setup);var lesson=(await f.Db.StudyCatalogLessons.SingleAsync()).Id;
        var saved=await svc.SaveAsync(new(null,"Europe/Istanbul",[Slot(lesson,1080,60)]),default);
        await svc.SaveAsync(new(saved.Version,"Europe/Istanbul",[Slot(lesson,1110,30)]),default);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>svc.SaveAsync(new(saved.Version,"Europe/Istanbul",[]),default));
        setup.Empty=true;var reopened=await svc.GetAsync(default);Assert.Equal("Matematik",reopened.Draft!.Slots[0].LessonName);Assert.Empty(reopened.Lessons);
        f.User.UserId=Guid.NewGuid();Assert.Null((await svc.GetAsync(default)).Draft);
        f.User.Roles=["Teacher"];await Assert.ThrowsAsync<BusinessRuleException>(()=>svc.GetAsync(default));
    }

    internal static WeeklySlotInput Slot(Guid lesson,int start,int duration)=>new(Guid.NewGuid(),lesson,DayOfWeek.Monday,start,duration);
    internal static CoachingWeeklyStudyDraftService Service(CurriculumManagementTests.Fixture f,Catalog setup)=>new(f.Db,new CoachingAccessPolicy(f.User),setup);
    internal sealed class Catalog(Coaching.Infrastructure.Data.CoachingDbContext db):ILearningSetupService
    {
        public bool Empty;
        public Task<LearningSetupView> GetAsync(CancellationToken ct)=>throw new NotSupportedException();
        public Task<LearningSetupView> SaveAsync(LearningSetupUpdate r,CancellationToken ct)=>throw new NotSupportedException();
        public async Task<LearningCatalogView> GetCatalogAsync(Guid? lessonId,int pageNumber,CancellationToken ct)=>new(null,
            Empty ? [] : await db.StudyCatalogLessons.Where(x=>x.IsActive).Select(x=>new LearningLessonView(x.Id,x.Name,x.GradeNumber,x.ExamCode,0,0)).ToArrayAsync(ct),new([],0,1,50),0,[]);
    }
    private sealed class NoAttachments:IAssignmentAttachmentStorage
    {
        public Task<AssignmentAttachmentUploadTicket> CreateUploadTicketAsync(Guid a,Guid s,Guid id,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<StoredAssignmentAttachment> StoreAsync(string key,Stream content,string type,long size,string hash,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string key,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task DeleteAsync(string key,CancellationToken ct=default)=>throw new NotSupportedException();
    }
}
