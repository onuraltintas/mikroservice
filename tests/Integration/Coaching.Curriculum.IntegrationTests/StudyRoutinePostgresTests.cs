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
using Microsoft.EntityFrameworkCore.Diagnostics;
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

    private const string AvailabilityChanged = "Çalışma saatlerin başka bir yerde değişti; yeniden yükle.", RoutineChanged = "Günlük düzenin başka bir yerde değişti; yeniden yükle.";

    private static CoachingStudyRoutineService Service(CoachingDbContext db, CurriculumManagementTests.Actor user, Guid? readable = null,
        ICoachingAdminScopeAuthorization? scope = null, bool sharing = true) =>
        new(db, new CoachingAccessPolicy(user), new CoachingStudyPlanAccess(new CoachingAccessPolicy(user), new Identity(readable ?? Guid.Empty, sharing),
            scope ?? new CurriculumManagementTests.Scope()));

    private static CoachingDbContext Intercepted(CurriculumPostgresTests.Pg pg, int saveCall, Func<Task> action) =>
        new(new DbContextOptionsBuilder<CoachingDbContext>(pg.Options).AddInterceptors(new BeforeSave(saveCall, action)).Options);

    private static async Task<(DayOfWeek, int, int)[]> StoredWindows(CurriculumPostgresTests.Pg pg, Guid student)
    {
        await using var db = new CoachingDbContext(pg.Options);
        return (await db.StudyAvailability.AsNoTracking().SingleAsync(x => x.StudentId == student)).Windows.Select(x => (x.Day, x.StartMinute, x.EndMinute)).ToArray();
    }

    private static (DayOfWeek, int, int)[] Derived(StudyRoutineAnswers answers) =>
        StudyRoutine.ToWindows(StudyRoutine.Validate(answers, true)).Windows.Select(x => (x.Day, x.StartMinute, x.EndMinute)).ToArray();

    private static async Task Conflict(Func<Task> action, string message)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(action);
        Assert.Equal(("StudyPlanning.Conflict", message), (error.Code, error.Message));
    }

    private static async Task<(int Version, int AvailabilityVersion, int StoredAvailabilityVersion, string Answers)> Stored(CurriculumPostgresTests.Pg pg, Guid student)
    {
        await using var db = new CoachingDbContext(pg.Options);
        var profile = await db.Set<StudyRoutineProfile>().AsNoTracking().SingleAsync(x => x.StudentId == student);
        var hours = await db.StudyAvailability.AsNoTracking().SingleAsync(x => x.StudentId == student);
        return (profile.Version, profile.AvailabilityVersion, hours.Version, profile.AnswersJson);
    }

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
            await Conflict(() => Service(db, user).ReplaceAsync(new(null, 0, NoSchool())), RoutineChanged);
            await Conflict(() => Service(db, user).ReplaceAsync(new(0, null, NoSchool())), AvailabilityChanged);
            await Conflict(() => Service(db, user).ReplaceAsync(new(1, 0, NoSchool())), RoutineChanged);
            await Conflict(() => Service(db, user).ReplaceAsync(new(0, 1, NoSchool())), AvailabilityChanged);
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

        await using (var db = new CoachingDbContext(pg.Options)) await Conflict(() => Service(db, user).ReplaceAsync(new(1, 1, Morning())), AvailabilityChanged);
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
        // The profile holds the availability version that was actually saved.
        var stored = await Stored(pg, student);
        Assert.Equal((2, 3, 3), (stored.Version, stored.AvailabilityVersion, stored.StoredAvailabilityVersion));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ARoutineReplacesFreeHoursEnteredBeforeItAndRejectsInvalidAnswers()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var user = pg.Data.User; user.Roles = ["Student"]; var student = user.UserId!.Value;
        await new CoachingStudyAvailabilityService(pg.Db, new CoachingAccessPolicy(user)).ReplaceAsync(new(null, null, [new(DayOfWeek.Monday, 1080, 1200)]));

        await using (var db = new CoachingDbContext(pg.Options)) await Conflict(() => Service(db, user).ReplaceAsync(new(null, null, Morning())), AvailabilityChanged);
        // Invalid answers are a 400 with the sentence for the student, and nothing changes.
        var controller = new StudyRoutineController(Service(new CoachingDbContext(pg.Options), user));
        Assert.Contains("en az 3 saat", Message(Assert.IsType<BadRequestObjectResult>(
            await controller.Replace(new(null, 0, Morning() with { SchoolEnd = 600 }))).Value));
        // Answers that leave no window: after school 18:00 until 19:00 with dinner 18:00–19:00; every weekday off; both without a weekend.
        foreach (var empty in new[]
        {
            Morning() with { SchoolEnd = 1020, AfterSchoolStart = 1080, LatestEnd = 1140, DinnerStart = 1080, DinnerEnd = 1140, Weekend = StudyWeekendDays.None },
            Morning() with { BusyDays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday], BusyDaysOff = true, Weekend = StudyWeekendDays.None },
        })
            Assert.Equal(StudyRoutine.NoWindowMessage, Message(Assert.IsType<BadRequestObjectResult>(await controller.Replace(new(null, 0, empty))).Value));
        // Decision: a chosen weekend with only the lunch hour is refused on its own.
        Assert.Equal(StudyRoutine.NoWeekendWindowMessage, Message(Assert.IsType<BadRequestObjectResult>(
            await controller.Replace(new(null, 0, Morning() with { WeekendStart = 780, WeekendEnd = 840 }))).Value));
        // A missing busy start can be answered.
        Assert.Equal("Kurs olan günlerde kaçtan sonra çalışabileceğini seç.", Message(Assert.IsType<BadRequestObjectResult>(
            await controller.Replace(new(null, 0, Morning() with { BusyDays = [DayOfWeek.Monday] }))).Value));
        Assert.Equal(new[] { (DayOfWeek.Monday, 1080, 1200) }, await StoredWindows(pg, student));
        await using (var db = new CoachingDbContext(pg.Options)) Assert.False(await db.Set<StudyRoutineProfile>().AnyAsync());

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
        Assert.NotNull(typeof(StudyRoutineController).GetMethod(nameof(StudyRoutineController.Replace))!
            .GetCustomAttributes(typeof(RequestSizeLimitAttribute), true).SingleOrDefault());
        // The student turned sharing with teachers off: the teacher's read is forbidden.
        Assert.Equal("Authorization.Forbidden", (await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, teacher, student, sharing: false).GetForStudentAsync(student))).Code);
        // An institution administrator reads students in her institution only.
        var admin = new CurriculumManagementTests.Actor { Roles = ["InstitutionAdmin"] };
        Assert.NotNull(await Service(db, admin, scope: new Institution(student)).GetForStudentAsync(student));
        Assert.Equal("Authorization.Forbidden", (await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, admin, scope: new Institution(Guid.NewGuid())).GetForStudentAsync(student))).Code);
        // The global admin screen shows the routine next to the free hours (read only).
        Assert.True((await new CoachingAdminStudyReader(db, new CurriculumManagementTests.Scope()).RoutineAsync(student, default))!.AvailabilityMatches);
        await Assert.ThrowsAsync<BusinessRuleException>(() => new CoachingAdminStudyReader(db, new CurriculumManagementTests.Scope { Global = false }).RoutineAsync(student, default));
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
            Assert.Equal("1.13", export.SchemaVersion);
            Assert.NotNull(export.StudyRoutine);
            Assert.Equal(StudySchoolShift.Morning, export.StudyRoutine.Answers.SchoolShift);
            Assert.Equal(Derived(Morning()), export.StudyRoutine.Windows.Select(x => (x.Day, x.StartMinute, x.EndMinute)));
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(export));
            Assert.Equal(900, json.RootElement.GetProperty("StudyRoutine").GetProperty("Answers").GetProperty("AfterSchoolStart").GetInt32());
        }

        var id = Guid.NewGuid(); var eventId = Guid.NewGuid(); var now = DateTime.UtcNow;
        await using (var db = new CoachingDbContext(pg.Options))   // the free hours, the routine and its plan preferences (Paket H4a)
            Assert.Equal(3, (await new CoachingErasureAssessmentService(db, TimeProvider.System).AssessAsync(new(eventId, id, student, now, true, PersonalDataScope.Coaching), default)).StudyPlanningRecordCount);
        await using (var db = new CoachingDbContext(pg.Options))
            await new CoachingErasureExecutionService(db, new NoAttachments(), TimeProvider.System).ExecuteAsync(new(eventId, id, student, now, PersonalDataScope.Coaching), default);
        await using (var db = new CoachingDbContext(pg.Options))
        {
            Assert.False(await db.Set<StudyRoutineProfile>().AnyAsync(x => x.StudentId == student));
            Assert.False(await db.StudyAvailability.AnyAsync(x => x.StudentId == student));
            Assert.True(await db.StudyAvailability.AnyAsync(x => x.StudentId == other.UserId!.Value));
            Assert.True(await db.Set<StudyRoutineProfile>().AnyAsync(x => x.StudentId == other.UserId!.Value));
        }
        // A stored row that can no longer be read is exported as it is instead of failing the export.
        await using (var db = new CoachingDbContext(pg.Options))
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE coaching.study_routine_profiles SET \"AnswersJson\" = {0}::jsonb WHERE \"StudentId\" = {1}", "{\"SchoolShift\": \"Evening\"}", other.UserId!.Value);
            var export = await new CoachingDataExportRepository(db).ExportStudentDataAsync(other.UserId!.Value, DateTimeOffset.UtcNow, default);
            Assert.Null(export.StudyRoutine);
            Assert.Contains("Evening", export.StudyRoutineUnreadableAnswersJson);
            Assert.NotNull(export.StudyAvailability);
        }
    }

    // Paket H4a: the tempo is saved with the answers (no new column) and returned.
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TheTempoIsSavedAndReturned()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var user = pg.Data.User; user.Roles = ["Student"]; var student = user.UserId!.Value;
        pg.Db.AddRange(Coaching.Domain.Profiles.CoachingStudentProfile.Create(student), StudentLearningContext.Create(student, 2026, LearningPurpose.Lgs, 2027, null, null));
        await pg.Db.SaveChangesAsync();
        Assert.Equal(StudyTempo.Balanced, (await Service(pg.Db, user).ReplaceAsync(new(null, null, Morning() with { Tempo = StudyTempo.Balanced }))).Answers.Tempo);
        await using (var db = new CoachingDbContext(pg.Options)) Assert.Equal(StudyTempo.Balanced, (await Service(db, user).GetAsync())!.Answers.Tempo);
        Assert.Contains("\"Tempo\": \"Balanced\"", (await Stored(pg, student)).Answers);
        StudyRoutineView cleared;
        await using (var db = new CoachingDbContext(pg.Options)) cleared = await Service(db, user).ReplaceAsync(new(0, 0, Morning()));
        Assert.Null(cleared.Answers.Tempo);
        await using (var db = new CoachingDbContext(pg.Options))
        {
            var bad = await new StudyRoutineController(Service(db, user)).Replace(new(1, 1, Morning() with { Tempo = (StudyTempo)9 }));
            Assert.Equal("Tempo Rahat, Dengeli ya da Yoğun olabilir.", Message(Assert.IsType<BadRequestObjectResult>(bad).Value));
        }
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task AFocusLeftFromYksDoesNotBlockSavingAndIsHidden()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var user = pg.Data.User; user.Roles = ["Student"]; var student = user.UserId!.Value;
        pg.Db.AddRange(Coaching.Domain.Profiles.CoachingStudentProfile.Create(student), StudentLearningContext.Create(student, 2026, LearningPurpose.Yks, 2027, "SAY", null));
        await pg.Db.SaveChangesAsync();
        var answers = Morning() with { ExamFocus = StudyExamFocus.Tyt };
        Assert.Equal(StudyExamFocus.Tyt, (await Service(pg.Db, user).ReplaceAsync(new(null, null, answers))).Answers.ExamFocus);

        // The student changes her exam to LGS: the saved focus is no longer shown, and saving the same answers again works.
        await pg.Db.Database.ExecuteSqlRawAsync("UPDATE coaching.student_learning_contexts SET \"Purpose\" = {0} WHERE \"StudentUserId\" = {1}", (int)LearningPurpose.Lgs, student);
        await using (var db = new CoachingDbContext(pg.Options)) Assert.Null((await Service(db, user).GetAsync())!.Answers.ExamFocus);
        await using (var db = new CoachingDbContext(pg.Options))
        {
            var ok = Assert.IsType<OkObjectResult>(await new StudyRoutineController(Service(db, user)).Replace(new(0, 0, answers)));
            Assert.NotNull(ok.Value);
        }
        var stored = await Stored(pg, student);
        Assert.Equal(1, stored.Version);
        Assert.Contains("\"ExamFocus\": null", stored.Answers);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task EditsBetweenReadingAndSavingAreConflictsAndRollBack()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var user = pg.Data.User; user.Roles = ["Student"]; var student = user.UserId!.Value;
        await Service(pg.Db, user).ReplaceAsync(new(null, null, Morning()));
        (DayOfWeek, int, int)[] handEdit = [(DayOfWeek.Monday, 1080, 1200)];

        // The free hours are edited by hand in another tab after the routine save read them: 409, nothing of the routine saved.
        await using (var db = Intercepted(pg, 1, async () =>
        {
            await using var other = new CoachingDbContext(pg.Options);
            await new CoachingStudyAvailabilityService(other, new CoachingAccessPolicy(user)).ReplaceAsync(new(0, null, [new(DayOfWeek.Monday, 1080, 1200)]));
        }))
            await Conflict(() => Service(db, user).ReplaceAsync(new(0, 0, NoSchool())), AvailabilityChanged);
        var stored = await Stored(pg, student);
        Assert.Equal((0, 0, 1), (stored.Version, stored.AvailabilityVersion, stored.StoredAvailabilityVersion));
        Assert.Contains("\"Morning\"", stored.Answers);
        Assert.Equal(handEdit, await StoredWindows(pg, student));

        // The routine changes in another tab after the free hours were written in this save: 409, and the free hours roll back.
        await using (var db = Intercepted(pg, 2, async () =>
        {
            await using var other = new CoachingDbContext(pg.Options);
            await other.Database.ExecuteSqlRawAsync("UPDATE coaching.study_routine_profiles SET \"Version\" = \"Version\" + 1 WHERE \"StudentId\" = {0}", student);
        }))
            await Conflict(() => Service(db, user).ReplaceAsync(new(0, 1, NoSchool())), RoutineChanged);
        stored = await Stored(pg, student);
        Assert.Equal((1, 0, 1), (stored.Version, stored.AvailabilityVersion, stored.StoredAvailabilityVersion));
        Assert.Equal(handEdit, await StoredWindows(pg, student));

        await using (var db = new CoachingDbContext(pg.Options))
            Assert.True((await Service(db, user).ReplaceAsync(new(1, 1, NoSchool()))).AvailabilityMatches);
        stored = await Stored(pg, student);
        Assert.Equal((2, 2, 2), (stored.Version, stored.AvailabilityVersion, stored.StoredAvailabilityVersion));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TwoFirstSavesAtOnceGiveAConflictNotAnError()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var user = pg.Data.User; user.Roles = ["Student"]; var student = user.UserId!.Value;

        // Another tab creates the free hours first.
        await using (var db = Intercepted(pg, 1, async () =>
        {
            await using var other = new CoachingDbContext(pg.Options);
            await new CoachingStudyAvailabilityService(other, new CoachingAccessPolicy(user)).ReplaceAsync(new(null, null, [new(DayOfWeek.Monday, 1080, 1200)]));
        }))
        {
            var result = Assert.IsType<ConflictObjectResult>(await new StudyRoutineController(Service(db, user)).Replace(new(null, null, Morning())));
            Assert.Equal(AvailabilityChanged, Message(result.Value));
        }
        // Another tab creates the routine first, after this save wrote the free hours: those roll back.
        await using (var db = Intercepted(pg, 2, async () =>
        {
            await using var other = new CoachingDbContext(pg.Options);
            var first = StudyRoutineProfile.Create(student); first.Replace("{}", 0); other.Add(first); await other.SaveChangesAsync();
        }))
        {
            var result = Assert.IsType<ConflictObjectResult>(await new StudyRoutineController(Service(db, user)).Replace(new(null, 0, Morning())));
            Assert.Equal(RoutineChanged, Message(result.Value));
        }
        Assert.Equal(new[] { (DayOfWeek.Monday, 1080, 1200) }, await StoredWindows(pg, student));
        await using (var db = new CoachingDbContext(pg.Options))
            Assert.Equal(0, (await db.StudyAvailability.AsNoTracking().SingleAsync(x => x.StudentId == student)).Version);
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

    // ---- Paket H4a (H4b review): plan preferences (exam focus K6 and tempo) that never touch the free hours ----

    private static CoachingStudyPlanPreferencesService Preferences(CoachingDbContext db, CurriculumManagementTests.Actor user, Guid? readable = null,
        ICoachingAdminScopeAuthorization? scope = null) =>
        new(db, new CoachingAccessPolicy(user), new CoachingStudyPlanAccess(new CoachingAccessPolicy(user), new Identity(readable ?? Guid.Empty),
            scope ?? new CurriculumManagementTests.Scope()));

    private static async Task<(int Version, string Windows)> Hours(CurriculumPostgresTests.Pg pg, Guid student)
    {
        await using var db = new CoachingDbContext(pg.Options);
        var hours = await db.StudyAvailability.AsNoTracking().SingleAsync(x => x.StudentId == student);
        return (hours.Version, string.Join(",", hours.Windows.Select(x => $"{x.Day}{x.StartMinute}-{x.EndMinute}")));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task PlanPreferencesAreSavedWithoutTouchingTheFreeHours()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var user = pg.Data.User; user.Roles = ["Student"]; var student = user.UserId!.Value;
        pg.Db.AddRange(Coaching.Domain.Profiles.CoachingStudentProfile.Create(student), StudentLearningContext.Create(student, 2026, LearningPurpose.Yks, 2027, "SAY", null));
        // Hand-made free hours and no routine: the usual case.
        await new CoachingStudyAvailabilityService(pg.Db, new CoachingAccessPolicy(user)).ReplaceAsync(new(null, null, [new(DayOfWeek.Monday, 1080, 1200)]));
        var before = await Hours(pg, student);

        await using (var db = new CoachingDbContext(pg.Options))
        {
            var empty = await Preferences(db, user).GetAsync();
            Assert.Equal(((StudyExamFocus?)null, (StudyTempo?)null, (int?)null), (empty.ExamFocus, empty.Tempo, empty.Version));
        }
        StudyPlanPreferencesView saved;
        await using (var db = new CoachingDbContext(pg.Options)) saved = await Preferences(db, user).ReplaceAsync(new(StudyExamFocus.Ayt, StudyTempo.Intense));
        Assert.Equal((StudyExamFocus.Ayt, StudyTempo.Intense), (saved.ExamFocus!.Value, saved.Tempo!.Value));
        await using (var db = new CoachingDbContext(pg.Options))
        {
            var read = Assert.IsType<OkObjectResult>(await new StudyPlanPreferencesController(Preferences(db, user)).Get());
            var view = (StudyPlanPreferencesView)read.Value!.GetType().GetProperty("data")!.GetValue(read.Value)!;
            Assert.Equal((StudyExamFocus.Ayt, StudyTempo.Intense), (view.ExamFocus!.Value, view.Tempo!.Value));
            Assert.False(await db.Set<StudyRoutineProfile>().AnyAsync());
        }
        Assert.Equal(before, await Hours(pg, student));
        // A second save replaces both values (null clears); the free hours stay as they are.
        await using (var db = new CoachingDbContext(pg.Options)) saved = await Preferences(db, user).ReplaceAsync(new(null, StudyTempo.Balanced));
        Assert.Equal(((StudyExamFocus?)null, (StudyTempo?)StudyTempo.Balanced), (saved.ExamFocus, saved.Tempo));
        Assert.Equal(before, await Hours(pg, student));
        await using (var db = new CoachingDbContext(pg.Options)) Assert.Equal(1, await db.Set<StudyPlanPreferences>().CountAsync());

        // Unknown names are refused; a focus for a student who does not prepare for YKS is cleared without an error.
        await using (var db = new CoachingDbContext(pg.Options))
        {
            var api = new StudyPlanPreferencesController(Preferences(db, user));
            Assert.Equal("Tempo Rahat, Dengeli ya da Yoğun olabilir.", Message(Assert.IsType<BadRequestObjectResult>(await api.Replace(new(null, (StudyTempo)9))).Value));
            Assert.Equal("TYT ve AYT ağırlığını seç.", Message(Assert.IsType<BadRequestObjectResult>(await api.Replace(new((StudyExamFocus)9, null))).Value));
        }
        await pg.Db.Database.ExecuteSqlRawAsync("UPDATE coaching.student_learning_contexts SET \"Purpose\" = {0} WHERE \"StudentUserId\" = {1}", (int)LearningPurpose.Lgs, student);
        await using (var db = new CoachingDbContext(pg.Options))
        {
            var lgs = await Preferences(db, user).ReplaceAsync(new(StudyExamFocus.Tyt, StudyTempo.Relaxed));
            Assert.Equal(((StudyExamFocus?)null, (StudyTempo?)StudyTempo.Relaxed), (lgs.ExamFocus, lgs.Tempo));
        }
        // The student route is hers only and rate-limited; the staff and admin routes only read.
        Assert.Equal("Student", Assert.Single(typeof(StudyPlanPreferencesController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Roles);
        Assert.Equal("study-planning-write", Assert.Single(typeof(StudyPlanPreferencesController).GetMethod(nameof(StudyPlanPreferencesController.Replace))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), true).Cast<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()).PolicyName);
        Assert.All(typeof(CoachingStaffStudyPlanPreferencesController).GetMethods().SelectMany(x => x.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute), true))
            .Cast<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>(), x => Assert.Equal(new[] { "GET" }, x.HttpMethods));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ARoutineAndThePlanPreferencesStayInStep()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var user = pg.Data.User; user.Roles = ["Student"]; var student = user.UserId!.Value;
        pg.Db.AddRange(Coaching.Domain.Profiles.CoachingStudentProfile.Create(student), StudentLearningContext.Create(student, 2026, LearningPurpose.Yks, 2027, "SAY", null));
        await pg.Db.SaveChangesAsync();
        // Saving the routine saves its focus and tempo as the plan preferences.
        await Service(pg.Db, user).ReplaceAsync(new(null, null, Morning() with { ExamFocus = StudyExamFocus.Tyt, Tempo = StudyTempo.Balanced }));
        await using (var db = new CoachingDbContext(pg.Options))
        {
            var prefs = await Preferences(db, user).GetAsync();
            Assert.Equal((StudyExamFocus.Tyt, StudyTempo.Balanced), (prefs.ExamFocus!.Value, prefs.Tempo!.Value));
        }
        var hours = await Hours(pg, student);
        // Saving the preferences changes what the routine shows, but not the routine's version or the free hours.
        await using (var db = new CoachingDbContext(pg.Options)) await Preferences(db, user).ReplaceAsync(new(StudyExamFocus.Ayt, StudyTempo.Intense));
        StudyRoutineView routine;
        await using (var db = new CoachingDbContext(pg.Options)) routine = (await Service(db, user).GetAsync())!;
        Assert.Equal((StudyExamFocus.Ayt, StudyTempo.Intense, 0, true), (routine.Answers.ExamFocus!.Value, routine.Answers.Tempo!.Value, routine.Version, routine.AvailabilityMatches));
        Assert.Equal(hours, await Hours(pg, student));
        // Saving the routine again as it was read keeps them; a new tempo in the routine is the new preference.
        await using (var db = new CoachingDbContext(pg.Options)) await Service(db, user).ReplaceAsync(new(0, hours.Version, routine.Answers));
        await using (var db = new CoachingDbContext(pg.Options)) Assert.Equal(StudyTempo.Intense, (await Preferences(db, user).GetAsync()).Tempo);
        await using (var db = new CoachingDbContext(pg.Options))
        {
            var current = (await Service(db, user).GetAsync())!;
            await Service(db, user).ReplaceAsync(new(current.Version, current.CurrentAvailabilityVersion, current.Answers with { Tempo = StudyTempo.Relaxed }));
        }
        await using (var db = new CoachingDbContext(pg.Options))
        {
            var prefs = await Preferences(db, user).GetAsync();
            Assert.Equal((StudyExamFocus.Ayt, StudyTempo.Relaxed), (prefs.ExamFocus!.Value, prefs.Tempo!.Value));
        }

        // Teachers with study access, institution administrators of her institution and the global admin read them; nobody else.
        var teacher = new CurriculumManagementTests.Actor { Roles = ["Teacher"] };
        await using (var db = new CoachingDbContext(pg.Options))
        {
            Assert.Equal(StudyTempo.Relaxed, (await Preferences(db, teacher, readable: student).GetForStudentAsync(student)).Tempo);
            var ok = Assert.IsType<OkObjectResult>(await new CoachingStaffStudyPlanPreferencesController(Preferences(db, teacher, readable: student)).Get(student));
            Assert.NotNull(ok.Value);
            Assert.Equal(403, Assert.IsType<ObjectResult>(await new CoachingStaffStudyPlanPreferencesController(Preferences(db, teacher)).Get(student)).StatusCode);
            Assert.Equal("Authorization.Forbidden", (await Assert.ThrowsAsync<BusinessRuleException>(() => Preferences(db, teacher, readable: student).ReplaceAsync(new(null, StudyTempo.Intense)))).Code);
            Assert.Equal(StudyTempo.Relaxed, (await new CoachingAdminStudyReader(db, new CurriculumManagementTests.Scope()).PlanPreferencesAsync(student, default)).Tempo);
            await Assert.ThrowsAsync<BusinessRuleException>(() => new CoachingAdminStudyReader(db, new CurriculumManagementTests.Scope { Global = false }).PlanPreferencesAsync(student, default));
        }

        // Exported and erased with the student's coaching data.
        await using (var db = new CoachingDbContext(pg.Options))
        {
            var export = await new CoachingDataExportRepository(db).ExportStudentDataAsync(student, DateTimeOffset.UtcNow, default);
            Assert.Equal("1.13", export.SchemaVersion);
            Assert.Equal((StudyExamFocus.Ayt, StudyTempo.Relaxed), (export.StudyPlanPreferences!.ExamFocus!.Value, export.StudyPlanPreferences.Tempo!.Value));
            Assert.Equal(StudyTempo.Relaxed, export.StudyRoutine!.Answers.Tempo);
        }
        var id = Guid.NewGuid(); var eventId = Guid.NewGuid(); var now = DateTime.UtcNow;
        await using (var db = new CoachingDbContext(pg.Options))   // the free hours, the routine and the preferences
            Assert.Equal(3, (await new CoachingErasureAssessmentService(db, TimeProvider.System).AssessAsync(new(eventId, id, student, now, true, PersonalDataScope.Coaching), default)).StudyPlanningRecordCount);
        await using (var db = new CoachingDbContext(pg.Options))
            await new CoachingErasureExecutionService(db, new NoAttachments(), TimeProvider.System).ExecuteAsync(new(eventId, id, student, now, PersonalDataScope.Coaching), default);
        await using (var db = new CoachingDbContext(pg.Options)) Assert.False(await db.Set<StudyPlanPreferences>().AnyAsync(x => x.StudentId == student));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task PlanPreferencesMigrationAddsTheTableAndRollsBack()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(Current);
        Assert.False(await PreferencesTableExists(pg.Db));
        await pg.Db.Database.MigrateAsync();
        Assert.False(pg.Db.Database.HasPendingModelChanges());
        Assert.True(await PreferencesTableExists(pg.Db));
        var student = Guid.NewGuid();
        var row = StudyPlanPreferences.Create(student); row.Replace("Tyt", "Intense"); pg.Db.Add(row); await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();
        pg.Db.Add(StudyPlanPreferences.Create(student));
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => pg.Db.SaveChangesAsync()); pg.Db.ChangeTracker.Clear();
        await Assert.ThrowsAnyAsync<Exception>(() => pg.Db.Database.ExecuteSqlRawAsync("UPDATE coaching.study_plan_preferences SET \"Tempo\" = 'Fast'"));
        await Assert.ThrowsAnyAsync<Exception>(() => pg.Db.Database.ExecuteSqlRawAsync("UPDATE coaching.study_plan_preferences SET \"ExamFocus\" = 'Lgs'"));
        await pg.Db.GetService<IMigrator>().MigrateAsync(Current);
        Assert.False(await PreferencesTableExists(pg.Db));
        Assert.True(await TableExists(pg.Db));
        await pg.Db.Database.MigrateAsync();
        Assert.Empty(await pg.Db.Database.GetPendingMigrationsAsync());
    }

    private static Task<bool> PreferencesTableExists(CoachingDbContext db) => db.Database.SqlQueryRaw<bool>(
        "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema='coaching' AND table_name='study_plan_preferences') AS \"Value\"").SingleAsync();

    private static string Message(object? body) => (string)body!.GetType().GetProperty("message")!.GetValue(body)!;

    private static Task<bool> TableExists(CoachingDbContext db) => db.Database.SqlQueryRaw<bool>(
        "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema='coaching' AND table_name='study_routine_profiles') AS \"Value\"").SingleAsync();

    // Teacher study access: the teaching relationship with one student, and her "share progress with teachers" setting.
    private sealed class Identity(Guid student, bool sharing = true) : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid id, CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacher, IReadOnlyCollection<Guid> ids, Guid? institution, bool admin, CancellationToken ct)
            => student != Guid.Empty && ids.All(x => x == student) ? Task.FromResult(institution) : throw new BusinessRuleException("Authorization.Forbidden", "Denied");
        public Task<Guid?> AuthorizeTeacherStudyAccessAsync(Guid teacher, IReadOnlyCollection<Guid> ids, CancellationToken ct)
            => sharing ? AuthorizeTeacherTargetsAsync(teacher, ids, null, false, ct) : throw new BusinessRuleException("Authorization.Forbidden", "Öğrenci ilerlemesini paylaşmıyor.");
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewer, IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult(ids);
    }

    private sealed class Institution(Guid student) : ICoachingAdminScopeAuthorization
    {
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => Task.FromResult(new CoachingAdminScope(false, Guid.NewGuid(), [student]));
    }

    private sealed class BeforeSave(int call, Func<Task> action) : SaveChangesInterceptor
    {
        private int calls;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (++calls == call) await action();
            return result;
        }
    }

    private sealed class NoAttachments : IAssignmentAttachmentStorage
    {
        public Task<AssignmentAttachmentUploadTicket> CreateUploadTicketAsync(Guid a, Guid s, Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StoredAssignmentAttachment> StoreAsync(string key, Stream content, string type, long size, string hash, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
