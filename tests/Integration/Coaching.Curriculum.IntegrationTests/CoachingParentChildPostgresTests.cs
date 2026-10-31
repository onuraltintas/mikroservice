using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Management;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Coaching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

// V3 (parent panel): the child page of a linked parent reads the child's week, the 8-week adherence and the teacher's
// weekly notes and decisions; every read asks Identity's authorize-read (product Coaching).
public sealed class CoachingParentChildPostgresTests
{
    // Thursday 2026-11-05 12:00 in Istanbul; the week starts on Monday 2026-11-02.
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Monday = new(2026, 11, 2);
    private static DateTime At(DateOnly day, int hour) => day.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Utc).AddHours(-3);

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TheWeekShowsPlanOverdueWorkNextSessionCountdownAndWhatNeedsAttention()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var f = await Family.Create(pg);
        var plan = StudyPlanRevision.Create(f.Student, Guid.NewGuid(), 1, "Haftalık plan"); plan.Activate();
        var monday = StudyPlanTask.Create(plan, Monday, "Matematik", 30); monday.Complete(30);
        var tuesday = StudyPlanTask.Create(plan, Monday.AddDays(1), "Paragraf tekrarı", 30);
        var friday = StudyPlanTask.Create(plan, Monday.AddDays(4), "Fizik", 30);
        var late = Assignment.Create(f.Teacher, "Problemler", At(Monday.AddDays(1), 18)); late.AssignToStudents([f.Student]);
        var coming = Assignment.Create(f.Teacher, "Geometri", At(Monday.AddDays(8), 18)); coming.AssignToStudents([f.Student]);
        var missed = CoachingSession.Create(f.Teacher, "Pazartesi görüşmesi", At(Monday, 10), SessionType.OneOnOne); missed.AddStudent(f.Student);
        missed.RecordAttendance(f.Student, AttendanceStatus.Absent, null, At(Monday, 12));
        var next = CoachingSession.Create(f.Teacher, "Haftalık görüşme", At(Monday.AddDays(4), 10), SessionType.OneOnOne); next.AddStudent(f.Student);
        var context = StudentLearningContext.Create(f.Student, 2026, LearningPurpose.Yks, 2027, "SAY", null);
        context.SetTargetExamDate(new DateOnly(2027, 6, 19), DateOnly.FromDateTime(Now.UtcDateTime));
        var fee = CoachingFeeAgreement.Create(Guid.NewGuid(), f.Student, null, f.Teacher, f.Teacher, "Ekim koçluk ücreti", 1250m,
            [new CoachingFeeInstallmentInput(new DateOnly(2026, 10, 20), 1250m)], Now.UtcDateTime.AddDays(-30));
        pg.Db.AddRange(plan, monday, tuesday, friday, late, coming, missed, next, context, fee);
        await pg.Db.SaveChangesAsync();
        pg.Db.Entry(monday).Property(x => x.CompletedAt).CurrentValue = At(Monday, 20);
        // Enrolled at the start of the school year: last week's reflection is still owed.
        pg.Db.Entry(await pg.Db.CoachingStudentProfiles.SingleAsync(x => x.UserId == f.Student)).Property(x => x.CreatedAt).CurrentValue =
            new DateTime(2026, 9, 7, 9, 0, 0, DateTimeKind.Utc);
        await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();

        var week = Data<ParentChildWeek>(await f.Api().Week(f.Student));

        Assert.Equal((Monday, 3, 1), (week.WeekStart, week.Planned, week.Completed));
        Assert.Equal([("assignment", "Problemler", Monday.AddDays(1)), ("study", "Paragraf tekrarı", Monday.AddDays(1))],
            week.Overdue.Select(x => (x.Kind, x.Title, x.Date)).OrderBy(x => x.Kind));
        Assert.Equal((At(Monday.AddDays(4), 10), "Haftalık görüşme", "Can Öğretmen"), (week.NextSession!.StartsAt, week.NextSession.Title, week.NextSession.TeacherName));
        Assert.Equal(("Elif'in sınavı", new DateOnly(2027, 6, 19), 226), (week.ExamCountdown!.Name, week.ExamCountdown.Date, week.ExamCountdown.DaysLeft));
        var attention = week.Attention.ToDictionary(x => x.Kind, x => x.Text);
        Assert.Equal(["missedSessions", "overdueAssignments", "overdueFees", "weeklyReviewWaiting"], attention.Keys.Order());
        Assert.Equal("Elif'in teslim tarihi geçmiş 1 ödevi var.", attention["overdueAssignments"]);
        Assert.Equal("Elif son 7 günde 1 seansa katılmadı.", attention["missedSessions"]);
        Assert.Equal("Vadesi geçmiş 1.250,00 TL ödeme var.", attention["overdueFees"]);
        Assert.Equal("Elif geçen haftanın değerlendirmesini henüz yazmadı.", attention["weeklyReviewWaiting"]);
    }

    // The student's own time zone decides "today" and the week.
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TheWeekIsTheStudentsOwnWeekAndQuietStudyIsNoticed()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var f = await Family.Create(pg, firstName: "Ada");
        var plan = StudyPlanRevision.Create(f.Student, Guid.NewGuid(), 1, "Plan"); plan.Activate();
        // Monday 01:00 UTC is still Sunday evening in New York: the New York week started on 26 October.
        pg.Db.AddRange(plan, StudyAvailability.Create(f.Student, "America/New_York"),
            StudyPlanTask.Create(plan, new DateOnly(2026, 10, 27), "Geçen hafta", 30), StudyPlanTask.Create(plan, Monday, "Bu hafta", 30));
        await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();

        var week = Data<ParentChildWeek>(await f.Api(new DateTimeOffset(2026, 11, 2, 1, 0, 0, TimeSpan.Zero)).Week(f.Student));

        Assert.Equal((new DateOnly(2026, 10, 26), 1, 0), (week.WeekStart, week.Planned, week.Completed));
        Assert.Equal("Ada henüz hiç çalışma kaydetmedi.", Assert.Single(week.Attention, x => x.Kind == "noStudy").Text);
        Assert.Null(week.NextSession); Assert.Null(week.ExamCountdown);
    }

    // Adherence reuses the T7 week totals (planned, completed, minutes) for the last 8 weeks, oldest first.
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task AdherenceListsTheLastWeeksOldestFirst()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var f = await Family.Create(pg);
        var plan = StudyPlanRevision.Create(f.Student, Guid.NewGuid(), 1, "Plan"); plan.Activate();
        var thisWeek = StudyPlanTask.Create(plan, Monday, "Matematik", 30); thisWeek.Complete(25, workedOn: Monday);
        var lastWeek = StudyPlanTask.Create(plan, Monday.AddDays(-6), "Türkçe", 40);
        var old = StudyPlanTask.Create(plan, Monday.AddDays(-49), "Fen", 20); old.Complete(20, workedOn: Monday.AddDays(-49));
        pg.Db.AddRange(plan, thisWeek, lastWeek, old); await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();

        var weeks = Data<IReadOnlyList<ParentChildAdherenceWeek>>(await f.Api().Adherence(f.Student));

        Assert.Equal(Enumerable.Range(0, 8).Select(i => Monday.AddDays(-7 * (7 - i))), weeks.Select(x => x.WeekStart));
        Assert.Equal((1, 1, 20L), (weeks[0].Planned, weeks[0].Completed, weeks[0].Minutes));
        Assert.Equal((1, 0), (weeks[6].Planned, weeks[6].Completed));
        Assert.Equal((1, 1, 25L), (weeks[7].Planned, weeks[7].Completed, weeks[7].Minutes));
        Assert.IsType<BadRequestObjectResult>(await f.Api().Adherence(f.Student, 13));
    }

    // The teacher's notes and decisions of recent weeks, read-only; the student's own reflection text is not shown.
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task WeeklyReviewsShowTheTeachersNotesAndDecisions()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var f = await Family.Create(pg);
        var reflection = WeeklyReflection.Create(f.Student, Monday.AddDays(-7)); reflection.Edit("Düzenli çalıştım", "Uyku", "Paragraf", 4);
        pg.Db.Add(reflection); await pg.Db.SaveChangesAsync();
        var note = WeeklyReflectionReview.Create(reflection, f.Teacher, "Güzel bir hafta, paragrafa devam.");
        var decision = WeeklyDecision.Create(Guid.NewGuid(), f.Student, f.Teacher, Monday.AddDays(-7));
        decision.Edit("Paragraf tekrarı", "Her gün 20 soru", Monday.AddDays(1), "Planned", At(Monday.AddDays(-1), 10));
        pg.Db.AddRange(note, decision); await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();

        var weeks = Data<IReadOnlyList<ParentChildWeeklyReview>>(await f.Api().WeeklyReviews(f.Student));

        Assert.Equal([Monday, Monday.AddDays(-7), Monday.AddDays(-14), Monday.AddDays(-21)], weeks.Select(x => x.WeekStart));
        var last = weeks[1];
        Assert.True(last.ReflectionWritten);
        Assert.Equal(("Can Öğretmen", "Güzel bir hafta, paragrafa devam."), (Assert.Single(last.TeacherNotes).TeacherName, last.TeacherNotes[0].Note));
        Assert.Equal(("Paragraf tekrarı", "Her gün 20 soru", Monday.AddDays(1), "Planned", true),
            (Assert.Single(last.Decisions).Title, last.Decisions[0].Note, last.Decisions[0].DueOn, last.Decisions[0].Status, last.Decisions[0].IsOverdue));
        Assert.False(weeks[0].ReflectionWritten); Assert.Empty(weeks[0].TeacherNotes);
        Assert.DoesNotContain("Düzenli çalıştım", System.Text.Json.JsonSerializer.Serialize(weeks));
    }

    // Coaching hidden from the parent, a revoked link and another parent's child: 403 on every endpoint.
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task EveryEndpointAsksIdentityAndRefusesHiddenOrRevokedChildren()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var f = await Family.Create(pg);
        Assert.IsType<OkObjectResult>(await f.Api().Week(f.Student));
        Assert.IsType<OkObjectResult>(await f.Api().Adherence(f.Student));
        Assert.IsType<OkObjectResult>(await f.Api().WeeklyReviews(f.Student));
        Assert.All(f.Accounts.ParentReadCalls, call => Assert.Equal((f.Parent, f.Student), call));

        f.Accounts.ParentReads.Clear();   // the child turned Coaching off for the parent, or the link was removed
        Assert.Equal(403, Assert.IsType<ObjectResult>(await f.Api().Week(f.Student)).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await f.Api().Adherence(f.Student)).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await f.Api().WeeklyReviews(f.Student)).StatusCode);

        f.Accounts.ParentReads.Add((f.Parent, f.Student));
        pg.Data.User.Roles = ["Teacher"];   // only the Parent role reads through these endpoints
        Assert.Equal(403, Assert.IsType<ObjectResult>(await f.Api().Week(f.Student)).StatusCode);
    }

    private static T Data<T>(IActionResult result)
    {
        var value = Assert.IsType<OkObjectResult>(result).Value!;
        return (T)value.GetType().GetProperty("data")!.GetValue(value)!;
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    private sealed class Family
    {
        public required CurriculumPostgresTests.Pg Pg { get; init; }
        public required Guid Student { get; init; }
        public required Guid Parent { get; init; }
        public required Guid Teacher { get; init; }
        public required Accounts Accounts { get; init; }

        public static async Task<Family> Create(CurriculumPostgresTests.Pg pg, string firstName = "Elif")
        {
            var student = Guid.NewGuid(); var parent = Guid.NewGuid(); var teacher = Guid.NewGuid();
            var accounts = new Accounts();
            accounts.Users[student] = new(student, firstName, "Yılmaz", "ogrenci@example.test", true, true, ["Student"], null);
            accounts.Users[parent] = new(parent, "Ayşe", "Yılmaz", "veli@example.test", true, true, ["Parent"], null);
            accounts.Users[teacher] = new(teacher, "Can", "Öğretmen", "koc@example.test", true, true, ["Teacher"], null);
            accounts.ParentReads.Add((parent, student));
            pg.Db.AddRange(CoachingStudentProfile.Create(student), CoachingTeacherProfile.Create(teacher),
                CoachingTeacherStudentAssignment.Create(teacher, student, null, null, teacher));
            await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();
            pg.Data.User.UserId = parent; pg.Data.User.Roles = ["Parent"];
            return new Family { Pg = pg, Student = student, Parent = parent, Teacher = teacher, Accounts = accounts };
        }

        public CoachingParentChildrenController Api(DateTimeOffset? now = null)
        {
            var clock = new Clock(now ?? Now);
            var directory = new OwnedCoachingDirectory(Pg.Db, Accounts, Pg.Data.User);
            var access = new CoachingAccessPolicy(Pg.Data.User);
            var today = new CoachingStudentTodayService(Pg.Db, new CoachingMotivationService(Pg.Db, directory, clock), clock);
            return new CoachingParentChildrenController(Pg.Db, new CoachingParentChildService(Pg.Db, access, directory, Accounts, today, clock), clock);
        }
    }

    private sealed class Accounts : ICoachingAccountDirectory
    {
        public Dictionary<Guid, CoachingAccount> Users { get; } = [];
        public HashSet<(Guid Parent, Guid Student)> ParentReads { get; } = [];
        public List<(Guid Parent, Guid Student)> ParentReadCalls { get; } = [];
        public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CoachingAccount>>(Users.Values.Where(x => ids.Contains(x.UserId)).ToArray());
        public Task<CoachingAccount?> FindByEmailAsync(string email, CancellationToken ct) => throw new NotSupportedException();
        public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id, CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<bool> IsActiveInstitutionAsync(Guid id, CancellationToken ct) => Task.FromResult(true);
        public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer, Guid target, CancellationToken ct) =>
            Task.FromResult<CoachingAgreementRepresentativeAuthorization?>(new(Guid.NewGuid(), "Mother"));
        public Task<CoachingParentReadAuthorization?> AuthorizeParentReadAsync(Guid parent, Guid student, CancellationToken ct)
        {
            ParentReadCalls.Add((parent, student));
            return Task.FromResult(ParentReads.Contains((parent, student)) ? new CoachingParentReadAuthorization(Guid.NewGuid(), "Mother") : null);
        }
    }
}
