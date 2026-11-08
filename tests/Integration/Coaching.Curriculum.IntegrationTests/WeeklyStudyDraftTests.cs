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
using Coaching.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class WeeklyStudyDraftTests
{
    [Fact]
    public async Task ApiIsStudentOnlyAndReturnsValidationAndConflictWithoutModifyingExistingPlan()
    {
        await using var f=new CurriculumManagementTests.Fixture();await f.Seed();f.User.Roles=["Student"];
        var controller=new WeeklyStudyDraftController(Service(f,new(f.Db)));
        Assert.Equal("Student",Assert.Single(typeof(WeeklyStudyDraftController).GetCustomAttributes(typeof(AuthorizeAttribute),true).Cast<AuthorizeAttribute>()).Roles);
        Assert.IsType<OkObjectResult>(await controller.Get(default));
        var lesson=(await f.Db.StudyCatalogLessons.SingleAsync()).Id;
        Assert.IsType<BadRequestObjectResult>(await controller.Save(new(null,"Europe/Istanbul",[Slot(lesson,1080,45)]),default));
        // E3 #34: a time zone sent by an older client is ignored; the draft is always in Türkiye time.
        Assert.IsType<OkObjectResult>(await controller.Save(new(null,"Invalid/Zone",[Slot(lesson,1080,30)]),default));
        Assert.Equal("Europe/Istanbul",(await f.Db.Set<WeeklyStudyDraft>().AsNoTracking().SingleAsync()).TimeZoneId);
        Assert.IsType<ConflictObjectResult>(await controller.Save(new(null,"Europe/Istanbul",[]),default));
        f.User.Roles=["Teacher"];Assert.Equal(403,Assert.IsType<ObjectResult>(await controller.Get(default)).StatusCode);
    }
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
        await using var f=await CurriculumPostgresTests.Pg.Create("20261020065330_MebiLgsProgramCatalog");await f.Data.Seed(legacyPlanSchema: true);f.Data.User.Roles=["Student"];
        var before=JsonSerializer.Serialize(await CurriculumManagementTests.LegacyStudyTasks(f.Db).AsNoTracking().ToArrayAsync());
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
    [InlineData(1080,45)] [InlineData(1085,30)] [InlineData(1410,60)] [InlineData(-30,30)] [InlineData(1080,0)] [InlineData(2147483640,60)]
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

    // Paket G #41 (07.11.2026): the page tells the proposal each chosen course's priority and its questions in the exam.
    [Fact]
    public async Task PageGivesTheChosenCoursesPriorityAndYksQuestionCount()
    {
        await using var f=new CurriculumManagementTests.Fixture();await f.Seed();f.User.Roles=["Student"];var student=f.User.UserId!.Value;
        (StudyCatalogLesson Lesson,StudyCatalogTopic Topic) Course(string name,string exam,string test)
        {
            var lesson=StudyCatalogLesson.Create("test","g41-"+test,name,null,exam);lesson.SetActive(true);
            var unit=StudyCatalogUnit.Create("test","g41-unit-"+test,lesson.Id,name,1);unit.SetActive(true);
            var topic=StudyCatalogTopic.Create("test","g41-topic-"+test,lesson.Id,unit.Id,name+" konusu",null,1);topic.SetActive(true);
            f.Db.AddRange(lesson,unit,topic,StudyCatalogYksScope.Create(topic.Id,exam,"SAY",test,null,"test"));return (lesson,topic);
        }
        var turkish=Course("Türkçe (TYT)","TYT","TYT.TUR");var biology=Course("Biyoloji (AYT)","AYT","AYT.FEN");
        var context=StudentLearningContext.Create(student,2026,LearningPurpose.Yks,2027,"SAY",null);f.Db.Add(context);
        context.ReplaceSelections([StudentCoursePreference.Create(context.Id,turkish.Lesson.Id,2,null),StudentCoursePreference.Create(context.Id,biology.Lesson.Id,1,null)],[],false);
        await f.Db.SaveChangesAsync();
        var courses=(await Service(f,new(f.Db)).GetAsync(default)).Courses!.ToDictionary(x=>x.LessonId);
        Assert.Equal(2,courses.Count);
        Assert.Equal(new WeeklyCourseRank(turkish.Lesson.Id,2,40m),courses[turkish.Lesson.Id]);
        Assert.Equal(new WeeklyCourseRank(biology.Lesson.Id,1,13m),courses[biology.Lesson.Id]);
        // Without a learning context the page has no ranks; the proposal keeps its own defaults.
        f.User.UserId=Guid.NewGuid();Assert.Empty((await Service(f,new(f.Db)).GetAsync(default)).Courses!);
    }

    // Paket H1 (K5, 08.11.2026): one day of the weekly routine holds at most 8 hours; 10 for a graduate who no longer attends school.
    [Fact]
    public async Task ADayHoldsAtMostEightHours_TenForAGraduate()
    {
        await using var f=new CurriculumManagementTests.Fixture();await f.Seed();f.User.Roles=["Student"];
        var lesson=(await f.Db.StudyCatalogLessons.SingleAsync()).Id;var svc=Service(f,new(f.Db));
        Assert.Equal(480,(await svc.GetAsync(default)).MaxDayMinutes);
        var eight=Day(lesson,DayOfWeek.Monday,480,8);
        var saved=await svc.SaveAsync(new(null,"Europe/Istanbul",[..eight,..Day(lesson,DayOfWeek.Tuesday,480,8)]),default);
        Assert.Equal(16,saved.Slots.Count);
        var over=await Assert.ThrowsAsync<StudyPlanValidationException>(()=>svc.SaveAsync(new(saved.Version,"Europe/Istanbul",[..eight,Slot(lesson,1200,30)]),default));
        Assert.Equal("Pazartesi günü için en çok 8 saat çalışma koyabilirsin.",over.Message);
        Assert.Equal(16,(await svc.GetAsync(default)).Draft!.Slots.Count);
        Assert.IsType<BadRequestObjectResult>(await new WeeklyStudyDraftController(svc).Save(new(saved.Version,"Europe/Istanbul",[..eight,Slot(lesson,1200,30)]),default));
        // A graduate (lise or university) gets 10 hours; a university student keeps 8.
        var profile=Coaching.Domain.Profiles.CoachingStudentProfile.Create(f.User.UserId!.Value);
        profile.UpdateEducation(Coaching.Domain.Profiles.CoachingEducationStatus.UniversityStudent,null,null,null,null,null,null,null);
        f.Db.Add(profile);await f.Db.SaveChangesAsync();
        Assert.Equal(480,(await svc.GetAsync(default)).MaxDayMinutes);
        profile.UpdateEducation(Coaching.Domain.Profiles.CoachingEducationStatus.HighSchoolGraduate,null,null,null,null,null,null,2025);await f.Db.SaveChangesAsync();
        Assert.Equal(600,(await svc.GetAsync(default)).MaxDayMinutes);
        var ten=Day(lesson,DayOfWeek.Monday,420,10);
        saved=await svc.SaveAsync(new(saved.Version,"Europe/Istanbul",ten),default);Assert.Equal(10,saved.Slots.Count);
        var graduateOver=await Assert.ThrowsAsync<StudyPlanValidationException>(()=>svc.SaveAsync(new(saved.Version,"Europe/Istanbul",[..ten,Slot(lesson,1080,30)]),default));
        Assert.Equal("Pazartesi günü için en çok 10 saat çalışma koyabilirsin.",graduateOver.Message);
        Assert.Equal(600,WeeklyDayLimit.For(Coaching.Domain.Profiles.CoachingEducationStatus.UniversityGraduate));
        Assert.Equal(480,WeeklyDayLimit.For(null));Assert.Equal(480,WeeklyDayLimit.For(Coaching.Domain.Profiles.CoachingEducationStatus.Student));
    }

    // Review of PR #129: a draft saved above the limit stays editable; a day may not rise above max(limit, its saved total).
    [Fact]
    public async Task ADraftAlreadyOverTheLimitStaysEditable_AndADayNeverRisesAboveItsSavedTotal()
    {
        await using var f=new CurriculumManagementTests.Fixture();await f.Seed();f.User.Roles=["Student"];
        var lesson=await f.Db.StudyCatalogLessons.SingleAsync();var student=f.User.UserId!.Value;
        var old=WeeklyStudyDraft.Create(student);
        old.Replace("Europe/Istanbul",Enumerable.Range(0,10).Select(i=>new WeeklyDraftSlot(Guid.NewGuid(),lesson.Id,lesson.Name,lesson.ExamCode,DayOfWeek.Friday,420+i*60,60)).ToArray());
        f.Db.Add(old);await f.Db.SaveChangesAsync();f.Db.ChangeTracker.Clear();
        var svc=Service(f,new(f.Db));var page=await svc.GetAsync(default);
        Assert.Equal(10,page.Draft!.Slots.Count);
        var friday=page.Draft.Slots.Select(x=>new WeeklySlotInput(x.Id,x.LessonId,x.Day,x.StartMinute,x.DurationMinutes)).ToArray();
        // Another day is edited while Friday stays 2 hours over.
        var edited=await svc.SaveAsync(new(page.Draft.Version,"Europe/Istanbul",[..friday,..Day(lesson.Id,DayOfWeek.Monday,1080,2)]),default);
        Assert.Equal(12,edited.Slots.Count);
        // Friday may go down by one hour…
        var lower=await svc.SaveAsync(new(edited.Version,"Europe/Istanbul",friday[..9]),default);
        Assert.Equal(9,lower.Slots.Count);
        // …but never up again above the limit and its saved total.
        var raised=await Assert.ThrowsAsync<StudyPlanValidationException>(()=>svc.SaveAsync(new(lower.Version,"Europe/Istanbul",friday),default));
        Assert.Equal("Cuma günü için en çok 8 saat çalışma koyabilirsin.",raised.Message);
        Assert.Equal(9,(await svc.GetAsync(default)).Draft!.Slots.Count);
    }

    [Fact]
    public async Task ATeacherSaveFollowsTheStudentsDailyLimit()
    {
        await using var f=new CurriculumManagementTests.Fixture();await f.Seed();
        var lesson=(await f.Db.StudyCatalogLessons.SingleAsync()).Id;var student=Guid.NewGuid();
        f.User.Roles=["Teacher"];var access=new CoachingAccessPolicy(f.User);
        var svc=new CoachingWeeklyStudyDraftService(f.Db,access,new Catalog(f.Db),new Coaching.Application.Authorization.CoachingStudyPlanAccess(access,new Teaching(student),f.Scope));
        Assert.Equal(480,(await svc.GetForStudentAsync(student,default)).MaxDayMinutes);
        var eight=Day(lesson,DayOfWeek.Sunday,480,8);
        var saved=await svc.SaveForStudentAsync(student,new(null,"Europe/Istanbul",eight),default);
        var over=await Assert.ThrowsAsync<StudyPlanValidationException>(()=>svc.SaveForStudentAsync(student,new(saved.Version,"Europe/Istanbul",[..eight,Slot(lesson,1200,30) with {Day=DayOfWeek.Sunday}]),default));
        Assert.Equal("Pazar günü için en çok 8 saat çalışma konabilir.",over.Message);
        var profile=Coaching.Domain.Profiles.CoachingStudentProfile.Create(student);
        profile.UpdateEducation(Coaching.Domain.Profiles.CoachingEducationStatus.UniversityGraduate,null,null,null,null,null,null,2020);
        f.Db.Add(profile);await f.Db.SaveChangesAsync();
        Assert.Equal(600,(await svc.GetForStudentAsync(student,default)).MaxDayMinutes);
        Assert.Equal(9,(await svc.SaveForStudentAsync(student,new(saved.Version,"Europe/Istanbul",[..eight,Slot(lesson,1200,30) with {Day=DayOfWeek.Sunday}]),default)).Slots.Count);
    }
    // Review of PR #129: the teacher endpoint shows the daily-limit message; any other invalid input keeps the generic message.
    [Fact]
    public void TheTeacherEndpointPassesOnlyTheDailyLimitMessage()
    {
        var student=Guid.NewGuid();
        IActionResult Save(Exception error)=>new CoachingStaffStudyController(null!,null!,null!,null!,null!,new Failing(error),null!,null!)
            .SaveWeeklyDraft(student,new(null,null,[]),default).GetAwaiter().GetResult();
        var limit=Assert.IsType<BadRequestObjectResult>(Save(new StudyPlanValidationException("Pazar günü için en çok 8 saat çalışma konabilir.")));
        Assert.Equal("Pazar günü için en çok 8 saat çalışma konabilir.",Message(limit.Value));
        var other=Assert.IsType<BadRequestObjectResult>(Save(new ArgumentException("İç ayrıntı")));
        Assert.Equal("Öğrenci, tarih, plan ve süre alanlarını kontrol edin.",Message(other.Value));
    }
    internal static string? Message(object? body)=>body?.GetType().GetProperty("message")?.GetValue(body) as string;
    private sealed class Failing(Exception error):IWeeklyStudyDraftService
    {
        public Task<WeeklyDraftPage> GetAsync(CancellationToken ct)=>throw error;
        public Task<WeeklyDraftView> SaveAsync(WeeklyDraftInput request,CancellationToken ct)=>throw error;
        public Task<WeeklyDraftPage> GetForStudentAsync(Guid studentId,CancellationToken ct)=>throw error;
        public Task<WeeklyDraftView> SaveForStudentAsync(Guid studentId,WeeklyDraftInput request,CancellationToken ct)=>throw error;
    }
    private static WeeklySlotInput[] Day(Guid lesson,DayOfWeek day,int start,int hours)=>
        Enumerable.Range(0,hours).Select(i=>new WeeklySlotInput(Guid.NewGuid(),lesson,day,start+i*60,60)).ToArray();
    internal sealed class Teaching(Guid student):Coaching.Application.Interfaces.ICoachingIdentityAuthorizationClient
    {
        public Task<Coaching.Application.Interfaces.CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid id,CancellationToken ct)=>Task.FromResult<Coaching.Application.Interfaces.CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacher,IReadOnlyCollection<Guid> ids,Guid? institution,bool admin,CancellationToken ct)
            => ids.All(x=>x==student)?Task.FromResult(institution):throw new BusinessRuleException("Authorization.Forbidden","Denied");
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewer,IReadOnlyCollection<Guid> ids,CancellationToken ct)=>Task.FromResult(ids);
    }

    internal static WeeklySlotInput Slot(Guid lesson,int start,int duration)=>new(Guid.NewGuid(),lesson,DayOfWeek.Monday,start,duration);
    internal static CoachingWeeklyStudyDraftService Service(CurriculumManagementTests.Fixture f,Catalog setup)=>new(f.Db,new CoachingAccessPolicy(f.User),setup);
    internal sealed class Catalog(Coaching.Infrastructure.Data.CoachingDbContext db):ILearningSetupService
    {
        public bool Empty;
        public Task<LearningSetupView> GetAsync(CancellationToken ct)=>throw new NotSupportedException();
        public Task<LearningSetupView> GetForStudentAsync(Guid studentId,CancellationToken ct)=>throw new NotSupportedException();
        public Task<LearningSetupView> SaveAsync(LearningSetupUpdate r,CancellationToken ct)=>throw new NotSupportedException();
        public Task<LearningSetupImpact> PreviewAsync(LearningSetupImpactRequest r,CancellationToken ct)=>throw new NotSupportedException();
        public Task<LearningCatalogView> GetCatalogForStudentAsync(Guid studentId,Guid? lessonId,int pageNumber,CancellationToken ct)=>GetCatalogAsync(lessonId,pageNumber,ct);
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
