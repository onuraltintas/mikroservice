using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Contracts.Events.Coaching;

namespace Coaching.Curriculum.IntegrationTests;

// Roadmap Y7: every Sunday evening a linked parent gets one e-mail summary of the child's week.
// V3 (parent panel): one e-mail with a Koçluk and a Hızlı okuma section; a section only for a product the child uses and
// left visible to the parent; no sections, no e-mail.
public sealed class CoachingParentWeeklySummaryPostgresTests
{
    private static readonly DateOnly Monday = new(2026, 11, 2);

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ParentsGetOneSummaryOfTheChildsWeekOnSundayEvening()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var student = Guid.NewGuid(); var quiet = Guid.NewGuid(); var teacher = Guid.NewGuid(); var parent = Guid.NewGuid();
        var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Haftalık plan"); plan.Activate();
        var done = StudyPlanTask.Create(plan, Monday.AddDays(1), "Matematik", 30);
        var open = StudyPlanTask.Create(plan, Monday.AddDays(3), "Türkçe", 40);
        var nextWeek = StudyPlanTask.Create(plan, Monday.AddDays(9), "Fen", 30);
        done.Complete(30);
        var submitted = Assignment.Create(teacher, "Paragraf", At(Monday.AddDays(2), 18)); submitted.AssignToStudents([student]);
        var missed = Assignment.Create(teacher, "Problemler", At(Monday.AddDays(4), 18)); missed.AssignToStudents([student]);
        var coming = Assignment.Create(teacher, "Geometri", At(Monday.AddDays(8), 18)); coming.AssignToStudents([student]);
        submitted.SubmitAssignment(student);
        var held = CoachingSession.Create(teacher, "Haftalık görüşme", At(Monday.AddDays(2), 10), SessionType.OneOnOne); held.AddStudent(student);
        held.RecordAttendance(student, AttendanceStatus.Present, null, At(Monday.AddDays(2), 11));
        var upcoming = CoachingSession.Create(teacher, "Sonraki görüşme", At(Monday.AddDays(8), 10), SessionType.OneOnOne); upcoming.AddStudent(student);
        var exam = Exam.CreateStudentReported(student, "TYT Deneme 3", ExamType.Mock, At(Monday.AddDays(5), 9), 120m);
        exam.AddResult(ExamResult.Create(exam.Id, student, 87.5m));
        pg.Db.AddRange(plan, done, open, nextWeek, submitted, missed, coming, held, upcoming, exam);
        await pg.Db.SaveChangesAsync();
        var parents = new Parents(new(parent, "veli@example.test", "Ayşe Veli", student, "Ada Öğrenci"),
            new(Guid.NewGuid(), "sessiz@example.test", "Can Veli", quiet, "Sessiz Öğrenci"));
        var events = new Publisher();

        await Run(pg, parents, events, At(Monday.AddDays(2), 19));
        Assert.Empty(events.Summaries);
        await Run(pg, parents, events, At(Monday.AddDays(6), 19));
        await Run(pg, parents, events, At(Monday.AddDays(6), 20));

        var summary = Assert.Single(events.Summaries);
        Assert.Empty(events.Legacy);
        Assert.Equal((parent, "veli@example.test", "Ada Öğrenci", Monday, Monday.AddDays(6)),
            (summary.ParentUserId, summary.ParentEmail, summary.StudentName, summary.WeekStart, summary.WeekEnd));
        var coaching = summary.Coaching!;
        Assert.Null(summary.SpeedReading);
        Assert.Equal((2, 1), (coaching.StudyTasksPlanned, coaching.StudyTasksCompleted));
        Assert.Equal((2, 1, 1), (coaching.AssignmentsDue, coaching.AssignmentsSubmitted, coaching.AssignmentsOverdue));
        Assert.Equal((1, 1, 1), (coaching.SessionsHeld, coaching.SessionsAttended, coaching.SessionsUpcoming));
        Assert.Equal(("TYT Deneme 3", 87.5m), (coaching.LatestExamTitle, coaching.LatestExamScore));
    }

    // Roadmap D2: one rule for "done" (submitted or graded) and "overdue" (open work of an active assignment past its due time).
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task GradedWorkCountsAsDoneAndClosedAssignmentsAreNotOverdue()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var student = Guid.NewGuid(); var teacher = Guid.NewGuid(); var parent = Guid.NewGuid();
        var paper = Assignment.Create(teacher, "Kâğıtta teslim", At(Monday.AddDays(1), 18)); paper.AssignToStudents([student]); paper.GradeAssignment(student, 80);
        var closed = Assignment.Create(teacher, "Kapatılan", At(Monday.AddDays(2), 18)); closed.AssignToStudents([student]); closed.Complete();
        var missed = Assignment.Create(teacher, "Kaçırılan", At(Monday.AddDays(3), 18)); missed.AssignToStudents([student]);
        pg.Db.AddRange(paper, closed, missed); await pg.Db.SaveChangesAsync();
        var events = new Publisher();
        await Run(pg, new Parents(new CoachingParentSummaryRecipient(parent, "veli@example.test", "Veli", student, "Öğrenci")), events, At(Monday.AddDays(6), 19));
        var summary = Assert.Single(events.Summaries).Coaching!;
        Assert.Equal((3, 1, 1), (summary.AssignmentsDue, summary.AssignmentsSubmitted, summary.AssignmentsOverdue));
    }

    // Coaching audit K3: the summary follows the student's own time zone (as the study reminders do).
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TheSummaryWaitsForSundayEveningInTheStudentsOwnTimeZone()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var student = Guid.NewGuid(); var parent = Guid.NewGuid();
        var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Haftalık plan"); plan.Activate();
        var task = StudyPlanTask.Create(plan, Monday.AddDays(1), "Matematik", 30);
        pg.Db.AddRange(plan, task, StudyAvailability.Create(student, "America/New_York"));
        await pg.Db.SaveChangesAsync();
        var parents = new Parents(new CoachingParentSummaryRecipient(parent, "veli@example.test", "Veli", student, "Öğrenci"));
        var events = new Publisher();

        // Sunday 19:00 in Istanbul is Sunday 11:00 in New York: too early.
        await Run(pg, parents, events, At(Monday.AddDays(6), 19));
        Assert.Empty(events.Summaries);
        // Sunday 18:30 in New York (EST, UTC-5).
        await Run(pg, parents, events, Monday.AddDays(6).ToDateTime(new TimeOnly(23, 30), DateTimeKind.Utc));
        var summary = Assert.Single(events.Summaries);
        Assert.Equal((Monday, Monday.AddDays(6), 1), (summary.WeekStart, summary.WeekEnd, summary.Coaching!.StudyTasksPlanned));
    }

    // Coaching audit (CoachingParentWeeklySummary.cs:111): "today" and "now" of the counted tasks are the student's, not
    // Istanbul's. At 18:30 on Sunday in New York it is already Monday in Istanbul; the Sunday 20:00 task is still ahead.
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TasksStillAheadAreJudgedInTheStudentsOwnTimeZone()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var student = Guid.NewGuid(); var parent = Guid.NewGuid();
        var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Haftalık plan"); plan.Activate();
        var sunday = Monday.AddDays(6);
        var ahead = StudyPlanTask.Create(plan, sunday, "Akşam", 30); ahead.SetWeeklyPlacement(Guid.NewGuid(), "Türkçe", 20 * 60);
        var tuesday = StudyPlanTask.Create(plan, Monday.AddDays(1), "Salı", 30);
        pg.Db.AddRange(plan, ahead, tuesday, StudyAvailability.Create(student, "America/New_York"));
        await pg.Db.SaveChangesAsync();
        var events = new Publisher();

        await Run(pg, new Parents(new CoachingParentSummaryRecipient(parent, "veli@example.test", "Veli", student, "Öğrenci")), events,
            sunday.ToDateTime(new TimeOnly(23, 30), DateTimeKind.Utc));

        Assert.Equal((1, 0), (Assert.Single(events.Summaries).Coaching!.StudyTasksPlanned, events.Summaries[0].Coaching!.StudyTasksCompleted));
    }

    // Coaching audit K3: two service instances running at the same moment send one summary and neither run fails.
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TwoInstancesAtOnceSendOneSummary()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var parentIds = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToArray();
        var recipients = new List<CoachingParentSummaryRecipient>();
        foreach (var parent in parentIds)
        {
            var student = Guid.NewGuid();
            var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Plan"); plan.Activate();
            pg.Db.AddRange(plan, StudyPlanTask.Create(plan, Monday.AddDays(2), "Türkçe", 20));
            recipients.Add(new(parent, $"{parent:N}@example.test", "Veli", student, "Öğrenci"));
        }
        await pg.Db.SaveChangesAsync();
        var parents = new Parents(recipients.ToArray());
        var events = new Publisher();
        await Task.WhenAll(Run(pg, parents, events, At(Monday.AddDays(6), 19)), Run(pg, parents, events, At(Monday.AddDays(6), 19)));
        Assert.Equal(20, events.Summaries.Count);
        Assert.Equal(20, events.Summaries.Select(x => x.ParentUserId).Distinct().Count());
    }

    // K4 (31.10.2026): the summary goes out from Sunday 18:00; Sunday tasks still ahead of that moment are not missed yet.
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task SundayTasksStillAheadAreNotCountedAsMissed()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var student = Guid.NewGuid(); var parent = Guid.NewGuid();
        var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Haftalık plan"); plan.Activate();
        var sunday = Monday.AddDays(6);
        var morning = StudyPlanTask.Create(plan, sunday, "Sabah", 30); morning.SetWeeklyPlacement(Guid.NewGuid(), "Matematik", 10 * 60);
        var evening = StudyPlanTask.Create(plan, sunday, "Akşam", 30); evening.SetWeeklyPlacement(Guid.NewGuid(), "Türkçe", 20 * 60);
        var eveningDone = StudyPlanTask.Create(plan, sunday, "Akşam erken bitti", 30); eveningDone.SetWeeklyPlacement(Guid.NewGuid(), "Fen", 21 * 60);
        eveningDone.Complete(30);
        var tuesday = StudyPlanTask.Create(plan, Monday.AddDays(1), "Salı", 30);
        pg.Db.AddRange(plan, morning, evening, eveningDone, tuesday); await pg.Db.SaveChangesAsync();
        var events = new Publisher();

        await Run(pg, new Parents(new CoachingParentSummaryRecipient(parent, "veli@example.test", "Veli", student, "Öğrenci")), events, At(sunday, 19));

        var summary = Assert.Single(events.Summaries).Coaching!;
        Assert.Equal((3, 1), (summary.StudyTasksPlanned, summary.StudyTasksCompleted));
    }

    // V3: the Coaching section only when the child uses Coaching and left it visible; the speed-reading section from the
    // speed-reading service only when the child uses speed reading and left it visible; nothing to say, no e-mail.
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task EachSectionFollowsTheChildsProductsAndVisibility()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        Guid Student()
        {
            var id = Guid.NewGuid();
            var plan = StudyPlanRevision.Create(id, Guid.NewGuid(), 1, "Plan"); plan.Activate();
            pg.Db.AddRange(plan, StudyPlanTask.Create(plan, Monday.AddDays(1), "Türkçe", 20));
            return id;
        }
        var both = Student(); var hidden = Student(); var srOnly = Guid.NewGuid(); var srQuiet = Guid.NewGuid(); var srHidden = Student();
        var noCoaching = Student();
        await pg.Db.SaveChangesAsync();
        var speedReading = new SpeedReading();
        speedReading.Weeks[both] = new(95, 4, 12, 260m, 25m, 78.5m, 9, 21, 1);
        speedReading.Weeks[hidden] = new(40, 2, 5, 210m, null, 70m, null, null, 0);
        speedReading.Weeks[srOnly] = new(30, 1, 3, null, null, null, 3, 21, 0);
        speedReading.Weeks[srHidden] = new(30, 1, 3, null, null, null, 3, 21, 0);
        CoachingParentSummaryRecipient R(Guid student, bool coachingVisible, bool speedReadingVisible, bool hasCoaching, bool hasSpeedReading) =>
            new(Guid.NewGuid(), $"{student:N}@example.test", "Veli", student, "Öğrenci", coachingVisible, speedReadingVisible, hasCoaching, hasSpeedReading);
        var rows = new[]
        {
            R(both, true, true, true, true),          // both sections
            R(hidden, false, true, true, true),        // Coaching hidden from the parent: speed reading only
            R(srOnly, true, true, false, true),        // no Coaching product: speed reading only
            R(srQuiet, true, true, false, true),       // speed-reading service has nothing (204): no e-mail
            R(srHidden, true, false, true, true),      // speed reading hidden: Coaching only
            R(noCoaching, true, true, false, false),   // the child's Coaching access ended: no e-mail
        };
        var events = new Publisher();

        await Run(pg, new Parents(rows), events, At(Monday.AddDays(6), 19), speedReading);
        await Run(pg, new Parents(rows), events, At(Monday.AddDays(6), 21), speedReading);

        var byStudent = events.Summaries.ToDictionary(x => x.StudentUserId);
        Assert.Equal(new[] { both, hidden, srOnly, srHidden }.Order(), byStudent.Keys.Order());
        Assert.NotNull(byStudent[both].Coaching);
        Assert.Equal(new CoachingParentWeeklySpeedReadingSection(95, 4, 12, 260m, 25m, 78.5m, 9, 21, 1), byStudent[both].SpeedReading);
        Assert.Null(byStudent[hidden].Coaching); Assert.Equal(40, byStudent[hidden].SpeedReading!.Minutes);
        Assert.Null(byStudent[srOnly].Coaching); Assert.Equal(3, byStudent[srOnly].SpeedReading!.ProgramDay);
        Assert.NotNull(byStudent[srHidden].Coaching); Assert.Null(byStudent[srHidden].SpeedReading);
        // Speed reading is asked once per child and week, only where it is used and visible, with the child's own week.
        Assert.Equal(new[] { both, hidden, srOnly, srQuiet }.Order(), speedReading.Calls.Select(x => x.Student).Order());
        Assert.All(speedReading.Calls, call => Assert.Equal(Monday, call.WeekStart));
    }

    // V3: a speed-reading outage does not use up the week: the child is tried again in the next cycle.
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ASpeedReadingOutageIsRetriedInTheNextCycle()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var student = Guid.NewGuid(); var parent = Guid.NewGuid();
        var speedReading = new SpeedReading { Failing = true };
        speedReading.Weeks[student] = new(20, 1, 2, null, null, null, null, null, 0);
        var parents = new Parents(new CoachingParentSummaryRecipient(parent, "veli@example.test", "Veli", student, "Öğrenci", true, true, false, true));
        var events = new Publisher();

        await Run(pg, parents, events, At(Monday.AddDays(6), 19), speedReading);
        Assert.Empty(events.Summaries);
        speedReading.Failing = false;
        await Run(pg, parents, events, At(Monday.AddDays(6), 20), speedReading);
        Assert.Equal(20, Assert.Single(events.Summaries).SpeedReading!.Minutes);
    }

    // Coaching audit: recipients are read page by page from Identity, never all at once.
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task RecipientsAreReadPageByPage()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var rows = Enumerable.Range(0, 450).Select(_ => new CoachingParentSummaryRecipient(Guid.NewGuid(), "veli@example.test", "Veli",
            Guid.NewGuid(), "Öğrenci")).ToArray();
        var parents = new Parents(rows);

        await Run(pg, parents, new Publisher(), At(Monday.AddDays(6), 19));

        Assert.Equal([1, 2, 3], parents.Pages.Select(x => x.Page));
        Assert.All(parents.Pages, page => Assert.InRange(page.Size, 1, 200));
    }

    // Istanbul local time on the given day.
    private static DateTime At(DateOnly day, int hour) => day.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Utc).AddHours(-3);

    private static async Task Run(CurriculumPostgresTests.Pg pg, Parents parents, Publisher events, DateTime now, SpeedReading? speedReading = null)
    {
        await using var db = new CoachingDbContext(pg.Options);
        await new CoachingParentWeeklySummaryProcessor(db, parents, events, new Clock(now), speedReading ?? new SpeedReading()).ProcessAsync();
    }

    private sealed class Parents(params CoachingParentSummaryRecipient[] recipients) : ICoachingParentDirectory
    {
        public List<(int Page, int Size)> Pages { get; } = [];
        public Task<CoachingParentSummaryRecipientPage> GetWeeklySummaryRecipientsAsync(int pageNumber, int pageSize, CancellationToken ct)
        {
            lock (Pages) Pages.Add((pageNumber, pageSize));
            return Task.FromResult(new CoachingParentSummaryRecipientPage(recipients.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(), recipients.Length));
        }
        public Task<IReadOnlyList<CoachingParentLink>> GetParentLinksAsync(IReadOnlyCollection<Guid> studentUserIds, Guid? parentUserId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CoachingParentLink>>([]);
    }

    private sealed class SpeedReading : ISpeedReadingParentSummaryClient
    {
        public Dictionary<Guid, SpeedReadingParentWeek> Weeks { get; } = [];
        public List<(Guid Student, DateOnly WeekStart)> Calls { get; } = [];
        public bool Failing { get; set; }
        public Task<SpeedReadingParentWeek?> GetWeekAsync(Guid studentUserId, DateOnly weekStart, CancellationToken ct)
        {
            lock (Calls) Calls.Add((studentUserId, weekStart));
            if (Failing) throw new HttpRequestException("speed reading is down");
            return Task.FromResult(Weeks.GetValueOrDefault(studentUserId));
        }
    }

    private sealed class Publisher : ICoachingEventPublisher
    {
        public List<CoachingParentWeeklySummaryV2> Summaries { get; } = [];
        public List<CoachingParentWeeklySummaryV1> Legacy { get; } = [];
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            if (message is CoachingParentWeeklySummaryV2 summary) lock (Summaries) Summaries.Add(summary);
            if (message is CoachingParentWeeklySummaryV1 legacy) lock (Legacy) Legacy.Add(legacy);
            return Task.CompletedTask;
        }
    }

    private sealed class Clock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }
}
