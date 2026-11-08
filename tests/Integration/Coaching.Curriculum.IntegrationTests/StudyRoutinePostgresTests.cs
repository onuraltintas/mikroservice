using System.Text.Json;
using Coaching.API.Controllers;
using Coaching.Application.Attachments;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Privacy;
using Coaching.Infrastructure.Repositories;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Events.Privacy;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

/// <summary>
/// H3 (user decision K2, 08.11.2026): the routine profile is saved with the free hours it gives, in one save, each with its own
/// version check; teachers read it like the other study data; it is exported and erased with the student's coaching data.
/// Migration 20261120090000_StudyRoutineProfiles.
/// </summary>
public sealed class StudyRoutinePostgresTests
{
    private const string Previous = "20261115090000_EducationStatusesAndIstanbulTime", Current = "20261120090000_StudyRoutineProfiles";

    private static StudyRoutineAnswers Morning() => StudyRoutineTests.Morning();
    private static StudyRoutineAnswers NoSchool() => Morning() with { SchoolShift = StudySchoolShift.NoSchool, DayStart = 540, Weekend = StudyWeekendDays.None };

    private static CoachingStudyRoutineService Service(CoachingDbContext db, CurriculumManagementTests.Actor user, Guid? readable = null) =>
        new(db, new CoachingAccessPolicy(user), new CoachingStudyPlanAccess(new CoachingAccessPolicy(user), new Identity(readable ?? Guid.Empty), new CurriculumManagementTests.Scope()));

    private static async Task<(DayOfWeek, int, int)[]> StoredWindows(CurriculumPostgresTests.Pg pg, Guid student)
    {
        await using var db = new CoachingDbContext(pg.Options);
        return (await db.StudyAvailability.AsNoTracking().SingleAsync(x => x.StudentId == student)).Windows.Select(x => (x.Day, x.StartMinute, x.EndMinute)).ToArray();
    }

    private static (DayOfWeek, int, int)[] Derived(StudyRoutineAnswers answers) =>
        StudyRoutine.ToWindows(StudyRoutine.Validate(answers, true)).Windows.Select(x => (x.Day, x.StartMinute, x.EndMinute)).ToArray();

    private static async Task Conflict(Func<Task> action) =>
        Assert.Equal("StudyPlanning.Conflict", (await Assert.ThrowsAsync<BusinessRuleException>(action)).Code);

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task SavingTheRoutineReplacesTheFreeHoursAndChecksBothVersions()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var user = pg.Data.User; user.Roles = ["Student"]; var student = user.UserId!.Value;
        pg.Db.AddRange(Coaching.Domain.Profiles.CoachingStudentProfile.Create(student), StudentLearningContext.Create(student, 2026, LearningPurpose.Yks, 2027, "SAY", null));
        await pg.Db.SaveChangesAsync();

        Assert.Null(await Service(pg.Db, user).GetAsync());
        Assert.IsType<NotFoundObjectResult>(await new StudyRoutineController(Service(pg.Db, user)).Get());

        // First save: the free hours are created from the answers.
        var created = await Service(pg.Db, user).ReplaceAsync(new(null, null, Morning() with { ExamFocus = StudyExamFocus.Tyt }));
        Assert.Equal((0, 0, 0, true, 1), (created.Version, created.AvailabilityVersion, created.CurrentAvailabilityVersion!.Value, created.AvailabilityMatches, created.SchemaVersion));
        Assert.Equal(Derived(Morning()), await StoredWindows(pg, student));
        Assert.Equal(Derived(Morning()), created.Windows.Select(x => (x.Day, x.StartMinute, x.EndMinute)));
        Assert.Equal(7, created.DayBudgets.Count);
        Assert.Equal(StudyExamFocus.Tyt, created.Answers.ExamFocus);

        // A stale routine version or a stale (missing) availability version is refused and changes nothing.
        await using (var db = new CoachingDbContext(pg.Options))
        {
            await Conflict(() => Service(db, user).ReplaceAsync(new(null, 0, NoSchool())));
            await Conflict(() => Service(db, user).ReplaceAsync(new(0, null, NoSchool())));
            await Conflict(() => Service(db, user).ReplaceAsync(new(1, 0, NoSchool())));
            await Conflict(() => Service(db, user).ReplaceAsync(new(0, 1, NoSchool())));
        }
        Assert.Equal(Derived(Morning()), await StoredWindows(pg, student));
        var refused = await new StudyRoutineController(Service(new CoachingDbContext(pg.Options), user)).Replace(new(5, 0, NoSchool()));
        Assert.Equal(409, Assert.IsType<ConflictObjectResult>(refused).StatusCode);

        // Update: both versions advance and the free hours are replaced.
        StudyRoutineView updated;
        await using (var db = new CoachingDbContext(pg.Options)) updated = await Service(db, user).ReplaceAsync(new(0, 0, NoSchool()));
        Assert.Equal((1, 1, true), (updated.Version, updated.AvailabilityVersion, updated.AvailabilityMatches));
        Assert.Equal(Derived(NoSchool()), await StoredWindows(pg, student));
        Assert.Equal(StudySchoolShift.NoSchool, updated.Answers.SchoolShift);
        Assert.Null(updated.Answers.SchoolStart);

        // The student edits her free hours by hand: the routine no longer matches them.
        await using (var db = new CoachingDbContext(pg.Options))
            await new CoachingStudyAvailabilityService(db, new CoachingAccessPolicy(user)).ReplaceAsync(new(1, null, [new(DayOfWeek.Monday, 1080, 1200)]));
        StudyRoutineView? read;
        await using (var db = new CoachingDbContext(pg.Options)) read = await Service(db, user).GetAsync();
        Assert.NotNull(read);
        Assert.Equal((1, 1, 2, false), (read.Version, read.AvailabilityVersion, read.CurrentAvailabilityVersion!.Value, read.AvailabilityMatches));
        Assert.Equal(Derived(NoSchool()), read.Windows.Select(x => (x.Day, x.StartMinute, x.EndMinute)));   // the answers' windows, not the hand edit

        await using (var db = new CoachingDbContext(pg.Options)) await Conflict(() => Service(db, user).ReplaceAsync(new(1, 1, Morning())));
        await using (var db = new CoachingDbContext(pg.Options))
        {
            var again = await Service(db, user).ReplaceAsync(new(1, 2, Morning()));
            Assert.Equal((2, 3, true), (again.Version, again.AvailabilityVersion, again.AvailabilityMatches));
        }
        Assert.Equal(Derived(Morning()), await StoredWindows(pg, student));
        await using (var db = new CoachingDbContext(pg.Options))
        {
            var hours = await db.StudyAvailability.AsNoTracking().SingleAsync(x => x.StudentId == student);
            Assert.Equal((3, "Europe/Istanbul"), (hours.Version, hours.TimeZoneId));
            Assert.Equal(1, await db.Set<StudyRoutineProfile>().CountAsync());
        }
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ARoutineReplacesFreeHoursEnteredBeforeItAndRejectsInvalidAnswers()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var user = pg.Data.User; user.Roles = ["Student"]; var student = user.UserId!.Value;
        await new CoachingStudyAvailabilityService(pg.Db, new CoachingAccessPolicy(user)).ReplaceAsync(new(null, null, [new(DayOfWeek.Monday, 1080, 1200)]));

        await using (var db = new CoachingDbContext(pg.Options)) await Conflict(() => Service(db, user).ReplaceAsync(new(null, null, Morning())));
        // ExamFocus is for YKS only: without a YKS purpose it is refused with a sentence for the student (400).
        var controller = new StudyRoutineController(Service(new CoachingDbContext(pg.Options), user));
        var bad = Assert.IsType<BadRequestObjectResult>(await controller.Replace(new(null, 0, Morning() with { ExamFocus = StudyExamFocus.Ayt })));
        Assert.Contains("yalnız YKS", Message(bad.Value));
        Assert.Contains("en az 3 saat", Message(Assert.IsType<BadRequestObjectResult>(
            await controller.Replace(new(null, 0, Morning() with { SchoolEnd = 600 }))).Value));
        await using (var db = new CoachingDbContext(pg.Options))
        {
            db.AddRange(Coaching.Domain.Profiles.CoachingStudentProfile.Create(student), StudentLearningContext.Create(student, 2026, LearningPurpose.Lgs, 2027, null, null));
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<StudyPlanValidationException>(() => Service(db, user).ReplaceAsync(new(null, 0, Morning() with { ExamFocus = StudyExamFocus.Balanced })));
        }
        Assert.Equal(new[] { (DayOfWeek.Monday, 1080, 1200) }, await StoredWindows(pg, student));

        await using (var db = new CoachingDbContext(pg.Options))
        {
            var saved = await Service(db, user).ReplaceAsync(new(null, 0, Morning()));
            Assert.Equal((0, 1, true), (saved.Version, saved.AvailabilityVersion, saved.AvailabilityMatches));
        }
        Assert.Equal(Derived(Morning()), await StoredWindows(pg, student));
        // The answers are kept as a JSON object with readable names.
        await using (var db = new CoachingDbContext(pg.Options))
        {
            var json = await db.Database.SqlQueryRaw<string>("SELECT \"AnswersJson\"::text AS \"Value\" FROM coaching.study_routine_profiles").SingleAsync();
            Assert.Contains("\"SchoolShift\": \"Morning\"", json);
            Assert.Contains("\"Weekend\": \"Both\"", json);
        }
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TeachersReadTheRoutineWithStudyAccessAndCannotWriteIt()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var user = pg.Data.User; user.Roles = ["Student"]; var student = user.UserId!.Value;
        await Service(pg.Db, user).ReplaceAsync(new(null, null, Morning()));

        var teacher = new CurriculumManagementTests.Actor { Roles = ["Teacher"] };
        await using var db = new CoachingDbContext(pg.Options);
        var view = await Service(db, teacher, readable: student).GetForStudentAsync(student);
        Assert.NotNull(view); Assert.True(view.AvailabilityMatches); Assert.Equal(StudySchoolShift.Morning, view.Answers.SchoolShift);
        var ok = Assert.IsType<OkObjectResult>(await new CoachingStaffStudyRoutineController(Service(db, teacher, readable: student)).Get(student));
        Assert.NotNull(ok.Value);
        // No study access (another teacher, or the student hid her progress): 403.
        Assert.Equal("Authorization.Forbidden", (await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, teacher).GetForStudentAsync(student))).Code);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await new CoachingStaffStudyRoutineController(Service(db, teacher)).Get(student)).StatusCode);
        // A teacher cannot write a routine: the service is the student's own, and the staff route has no write.
        Assert.Equal("Authorization.Forbidden", (await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, teacher, student).ReplaceAsync(new(0, 0, NoSchool())))).Code);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await new StudyRoutineController(Service(db, teacher, student)).Get()).StatusCode);
        Assert.Equal("Student", Assert.Single(typeof(StudyRoutineController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Roles);
        Assert.Equal("Teacher,InstitutionAdmin,InstitutionOwner", Assert.Single(typeof(CoachingStaffStudyRoutineController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Roles);
        Assert.All(typeof(CoachingStaffStudyRoutineController).GetMethods().SelectMany(x => x.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute), true))
            .Cast<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>(), x => Assert.Equal(new[] { "GET" }, x.HttpMethods));
        Assert.Equal("study-planning-write", Assert.Single(typeof(StudyRoutineController).GetMethod(nameof(StudyRoutineController.Replace))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), true).Cast<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()).PolicyName);
        Assert.Equal(Derived(Morning()), await StoredWindows(pg, student));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TheRoutineIsExportedAndErasedWithTheStudentsCoachingData()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var user = pg.Data.User; user.Roles = ["Student"]; var student = user.UserId!.Value;
        await Service(pg.Db, user).ReplaceAsync(new(null, null, Morning()));
        var other = new CurriculumManagementTests.Actor { Roles = ["Student"] };
        await using (var db = new CoachingDbContext(pg.Options)) await Service(db, other).ReplaceAsync(new(null, null, NoSchool()));

        await using (var db = new CoachingDbContext(pg.Options))
        {
            var export = await new CoachingDataExportRepository(db).ExportStudentDataAsync(student, DateTimeOffset.UtcNow, default);
            Assert.Equal("1.12", export.SchemaVersion);
            Assert.NotNull(export.StudyRoutine);
            Assert.Equal(StudySchoolShift.Morning, export.StudyRoutine.Answers.SchoolShift);
            Assert.Equal(Derived(Morning()), export.StudyRoutine.Windows.Select(x => (x.Day, x.StartMinute, x.EndMinute)));
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(export));
            Assert.Equal(900, json.RootElement.GetProperty("StudyRoutine").GetProperty("Answers").GetProperty("AfterSchoolStart").GetInt32());
        }

        var id = Guid.NewGuid(); var eventId = Guid.NewGuid(); var now = DateTime.UtcNow;
        await using (var db = new CoachingDbContext(pg.Options))   // the free hours and the routine
            Assert.Equal(2, (await new CoachingErasureAssessmentService(db, TimeProvider.System).AssessAsync(new(eventId, id, student, now, true, PersonalDataScope.Coaching), default)).StudyPlanningRecordCount);
        await using (var db = new CoachingDbContext(pg.Options))
            await new CoachingErasureExecutionService(db, new NoAttachments(), TimeProvider.System).ExecuteAsync(new(eventId, id, student, now, PersonalDataScope.Coaching), default);
        await using (var db = new CoachingDbContext(pg.Options))
        {
            Assert.False(await db.Set<StudyRoutineProfile>().AnyAsync(x => x.StudentId == student));
            Assert.False(await db.StudyAvailability.AnyAsync(x => x.StudentId == student));
            Assert.True(await db.StudyAvailability.AnyAsync(x => x.StudentId == other.UserId!.Value));
            Assert.True(await db.Set<StudyRoutineProfile>().AnyAsync(x => x.StudentId == other.UserId!.Value));
        }
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigrationAddsTheTableAndRollsBack()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(Previous);
        Assert.False(await TableExists(pg.Db));
        await pg.Db.Database.MigrateAsync();
        Assert.False(pg.Db.Database.HasPendingModelChanges());
        Assert.Contains(Current, await pg.Db.Database.GetAppliedMigrationsAsync());
        Assert.True(await TableExists(pg.Db));
        var student = Guid.NewGuid();
        var profile = StudyRoutineProfile.Create(student); profile.Replace("{\"SchoolShift\":\"Morning\"}", 0);
        pg.Db.Add(profile); await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();
        // One profile per student; the answers are a JSON object; the versions are not negative.
        var duplicate = StudyRoutineProfile.Create(student); duplicate.Replace("{}", 0); pg.Db.Add(duplicate);
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => pg.Db.SaveChangesAsync()); pg.Db.ChangeTracker.Clear();
        await Assert.ThrowsAnyAsync<Exception>(() => pg.Db.Database.ExecuteSqlRawAsync("UPDATE coaching.study_routine_profiles SET \"AnswersJson\" = '[]'"));
        await Assert.ThrowsAnyAsync<Exception>(() => pg.Db.Database.ExecuteSqlRawAsync("UPDATE coaching.study_routine_profiles SET \"AvailabilityVersion\" = -1"));
        await Assert.ThrowsAnyAsync<Exception>(() => pg.Db.Database.ExecuteSqlRawAsync("UPDATE coaching.study_routine_profiles SET \"SchemaVersion\" = 0"));

        await pg.Db.GetService<IMigrator>().MigrateAsync(Previous);
        Assert.False(await TableExists(pg.Db));
        Assert.Equal(Previous, (await pg.Db.Database.GetAppliedMigrationsAsync()).Last());
        await pg.Db.Database.MigrateAsync();
        Assert.True(await TableExists(pg.Db));
        Assert.Empty(await pg.Db.Database.GetPendingMigrationsAsync());
    }

    private static string Message(object? body) => (string)body!.GetType().GetProperty("message")!.GetValue(body)!;

    private static Task<bool> TableExists(CoachingDbContext db) => db.Database.SqlQueryRaw<bool>(
        "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema='coaching' AND table_name='study_routine_profiles') AS \"Value\"").SingleAsync();

    private sealed class Identity(Guid student) : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid id, CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacher, IReadOnlyCollection<Guid> ids, Guid? institution, bool admin, CancellationToken ct)
            => student != Guid.Empty && ids.All(x => x == student) ? Task.FromResult(institution) : throw new BusinessRuleException("Authorization.Forbidden", "Denied");
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewer, IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult(ids);
    }

    private sealed class NoAttachments : IAssignmentAttachmentStorage
    {
        public Task<AssignmentAttachmentUploadTicket> CreateUploadTicketAsync(Guid a, Guid s, Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StoredAssignmentAttachment> StoreAsync(string key, Stream content, string type, long size, string hash, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
