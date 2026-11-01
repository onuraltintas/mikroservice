using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.Curriculum.IntegrationTests;

// T3 (31.10.2026): the student's "Bugün" screen reads one summary: the next step, today's work with overdue items,
// the exam countdown, streak, this week's tasks, the goal gap, a due weekly review and the next session.
public sealed class StudentTodayPostgresTests
{
    // 2026-10-21 (Wednesday) 11:00 Istanbul.
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 21);
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class NoReaders : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? requestedInstitutionId, bool isSystemAdministrator, CancellationToken ct) => Task.FromResult<Guid?>(null);
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }

    private static StudentTodayController Api(CurriculumPostgresTests.Pg pg, DateTimeOffset now)
    {
        var clock = new Clock(now);
        return new StudentTodayController(pg.Db, new CoachingAccessPolicy(pg.Data.User),
            new CoachingStudentTodayService(pg.Db, new CoachingMotivationService(pg.Db, new NoReaders(), clock), clock), clock);
    }

    private static async Task<StudentTodayView> Read(StudentTodayController api) =>
        Assert.IsType<StudentTodayView>(Assert.IsType<OkObjectResult>(await api.Get()).Value);

    private static PracticeExamInput Tyt() => new("TYT", [new("TUR", 30, 4, 6), new("MAT", 25, 8, 7), new("TAR", 4, 0, 1), new("COG", 4, 1, 0),
        new("FEL", 4, 0, 1), new("DIN", 5, 0, 0), new("FIZ", 4, 2, 1), new("KIM", 4, 1, 2), new("BIO", 4, 0, 2)]);

    private static void Practice(ExamResult result, PracticeExamInput input)
    {
        var totals = PracticeExamRules.Summarize(input);
        result.SetAnswerStatistics(totals.Correct, totals.Wrong, totals.Empty);
        result.SetPracticeExam(input, ExamType.YKS);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task CombinesNextStepTodayOverdueCountdownWeekGoalReviewAndSession()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var student = Guid.NewGuid(); var teacher = Guid.NewGuid();
        pg.Data.User.UserId = student; pg.Data.User.Roles = ["Student"];
        var profile = CoachingStudentProfile.Create(student); pg.Db.Add(profile);
        var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Aktif plan"); plan.Activate(); pg.Db.Add(plan);
        var morning = StudyPlanTask.Create(plan, Today, "Paragraf", 30); morning.Schedule(Today, 14 * 60);
        var tomorrow = StudyPlanTask.Create(plan, Today.AddDays(1), "Problemler", 45);
        var monday = StudyPlanTask.Create(plan, Today.AddDays(-2), "Geometri", 30); monday.Complete(30);
        var nextWeek = StudyPlanTask.Create(plan, Today.AddDays(7), "Fizik", 30);
        pg.Db.AddRange(morning, tomorrow, monday, nextWeek);
        var late = Assignment.Create(teacher, "Türev ödevi", Now.UtcDateTime.AddDays(-1)); late.AssignToStudent(student);
        var due = Assignment.Create(teacher, "Okuma ödevi", Now.UtcDateTime.AddHours(5)); due.AssignToStudent(student);
        pg.Db.AddRange(late, due);
        var context = StudentLearningContext.Create(student, 2026, LearningPurpose.Yks, 2027, "SAY", null);
        context.SetTargetExamDate(new DateOnly(2027, 6, 19), Today); pg.Db.Add(context);
        var goal = AcademicGoal.Create(student, "ODTÜ Bilgisayar", GoalCategory.ExamPreparation); goal.SetNetTarget("TYT", 90m); pg.Db.Add(goal);
        var exam = Exam.CreateStudentReported(student, "TYT Deneme 4", ExamType.YKS, Now.UtcDateTime.AddDays(-3), 500m);
        var result = ExamResult.Create(exam.Id, student, 300m); Practice(result, Tyt()); exam.AddResult(result); pg.Db.Add(exam);
        var future = Exam.CreateStudentReported(student, "Gelecek deneme", ExamType.YKS, Now.UtcDateTime.AddDays(3), 500m);
        var futureResult = ExamResult.Create(future.Id, student, 450m); Practice(futureResult, Tyt() with { Sections = [new("TUR", 40, 0, 0), new("MAT", 40, 0, 0),
            new("TAR", 5, 0, 0), new("COG", 5, 0, 0), new("FEL", 5, 0, 0), new("DIN", 5, 0, 0), new("FIZ", 7, 0, 0), new("KIM", 7, 0, 0), new("BIO", 6, 0, 0)] });
        future.AddResult(futureResult); pg.Db.Add(future);
        var session = CoachingSession.Create(teacher, "Haftalık görüşme", Now.UtcDateTime.AddDays(1), SessionType.OneOnOne); session.AddStudent(student);
        var other = CoachingSession.Create(teacher, "Başka öğrenci", Now.UtcDateTime.AddHours(2), SessionType.OneOnOne); other.AddStudent(Guid.NewGuid());
        pg.Db.AddRange(session, other);
        await pg.Db.SaveChangesAsync();
        pg.Db.Entry(monday).Property(x => x.CompletedAt).CurrentValue = Now.UtcDateTime.AddDays(-2);
        // A student enrolled at the start of the school year owes last week's reflection.
        pg.Db.Entry(profile).Property(x => x.CreatedAt).CurrentValue = new DateTime(2026, 9, 7, 9, 0, 0, DateTimeKind.Utc);
        await pg.Db.SaveChangesAsync();

        var view = await Read(Api(pg, Now));

        Assert.Equal((Today, "Europe/Istanbul"), (view.Today, view.TimeZoneId));
        // An overdue assignment comes first; it is marked as overdue.
        Assert.Equal(("assignment", late.Id, true), (view.NextStep!.Item.Kind, view.NextStep.Item.RecordId, view.NextStep.Overdue));
        Assert.Equal([late.Id], view.Overdue.Select(x => x.RecordId));
        Assert.Equal([morning.Id, due.Id], view.Items.Select(x => x.RecordId));
        Assert.Equal((2, 1, 2), (view.TodayCount, view.OverdueCount, view.UpcomingCount));
        Assert.Equal(("Sınavın", new DateOnly(2027, 6, 19), 241), (view.Exam!.Name, view.Exam.Date, view.Exam.DaysLeft));
        // This week (Monday 19 October – Sunday 25 October): Geometri done, Paragraf and Problemler planned.
        Assert.Equal((new DateOnly(2026, 10, 19), 3, 1), (view.Week.WeekStart, view.Week.Planned, view.Week.Completed));
        // Last study day was Monday: the streak has ended, the longest run stays 1.
        Assert.Equal((0, 1, false), (view.Streak.CurrentStreak, view.Streak.LongestStreak, view.Streak.ActiveToday));
        // The future-dated exam does not count: the latest past TYT is 80 net against a 90 net target.
        Assert.Equal(("ODTÜ Bilgisayar", "TYT", 90m, 80m, 10m), (view.Goal!.Title, view.Goal.ExamCode, view.Goal.TargetNet, view.Goal.LatestNet, view.Goal.RemainingNet));
        Assert.True(view.WeeklyReview.Needed);
        Assert.Equal((session.Id, "Haftalık görüşme"), (view.NextSession!.Id, view.NextSession.Title));
        Assert.True(view.HasActivePlan);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task UsesTheStudentsOwnTimeZoneForToday()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var student = Guid.NewGuid(); pg.Data.User.UserId = student; pg.Data.User.Roles = ["Student"];
        pg.Db.Add(CoachingStudentProfile.Create(student));
        pg.Db.Add(StudyAvailability.Create(student, "America/New_York"));
        var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Plan"); plan.Activate(); pg.Db.Add(plan);
        // 21 October 02:00 UTC is still 20 October (22:00) in New York.
        var evening = StudyPlanTask.Create(plan, new DateOnly(2026, 10, 20), "Akşam tekrarı", 30);
        var istanbulToday = StudyPlanTask.Create(plan, new DateOnly(2026, 10, 21), "Yarınki iş", 30);
        pg.Db.AddRange(evening, istanbulToday); await pg.Db.SaveChangesAsync();

        var view = await Read(Api(pg, new DateTimeOffset(2026, 10, 21, 2, 0, 0, TimeSpan.Zero)));

        Assert.Equal((new DateOnly(2026, 10, 20), "America/New_York"), (view.Today, view.TimeZoneId));
        Assert.Equal([evening.Id], view.Items.Select(x => x.RecordId));
        Assert.Equal((evening.Id, false), (view.NextStep!.Item.RecordId, view.NextStep.Overdue));
        Assert.Equal(1, view.UpcomingCount);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task EmptyStudentGetsAnEmptyButCompleteSummaryAndOtherRolesAreRefused()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var student = Guid.NewGuid(); pg.Data.User.UserId = student; pg.Data.User.Roles = ["Student"];
        pg.Db.Add(CoachingStudentProfile.Create(student));
        // Someone else's work never leaks in.
        var other = Guid.NewGuid(); var plan = StudyPlanRevision.Create(other, Guid.NewGuid(), 1, "Plan"); plan.Activate();
        pg.Db.AddRange(plan, StudyPlanTask.Create(plan, Today, "Başkasının görevi", 30));
        await pg.Db.SaveChangesAsync();
        var api = Api(pg, Now);

        var view = await Read(api);
        Assert.Null(view.NextStep); Assert.Empty(view.Items); Assert.Empty(view.Overdue);
        Assert.Equal((0, 0, 0), (view.TodayCount, view.OverdueCount, view.UpcomingCount));
        Assert.Null(view.Exam); Assert.Null(view.Goal); Assert.Null(view.NextSession);
        Assert.Equal((0, 0), (view.Week.Planned, view.Week.Completed));
        Assert.Equal(0, view.Streak.CurrentStreak);
        Assert.False(view.HasActivePlan);

        pg.Data.User.Roles = ["Teacher"];
        Assert.Equal(403, Assert.IsType<ObjectResult>(await api.Get()).StatusCode);
    }
}
