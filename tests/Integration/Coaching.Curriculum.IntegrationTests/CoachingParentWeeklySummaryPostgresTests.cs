using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Contracts.Events.Coaching;

namespace Coaching.Curriculum.IntegrationTests;

// Roadmap Y7: every Sunday evening a linked parent gets one e-mail summary of the child's week.
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
        Assert.Equal((parent, "veli@example.test", "Ada Öğrenci", Monday, Monday.AddDays(6)),
            (summary.ParentUserId, summary.ParentEmail, summary.StudentName, summary.WeekStart, summary.WeekEnd));
        Assert.Equal((2, 1), (summary.StudyTasksPlanned, summary.StudyTasksCompleted));
        Assert.Equal((2, 1, 1), (summary.AssignmentsDue, summary.AssignmentsSubmitted, summary.AssignmentsOverdue));
        Assert.Equal((1, 1, 1), (summary.SessionsHeld, summary.SessionsAttended, summary.SessionsUpcoming));
        Assert.Equal(("TYT Deneme 3", 87.5m), (summary.LatestExamTitle, summary.LatestExamScore));
    }

    // Istanbul local time on the given day.
    private static DateTime At(DateOnly day, int hour) => day.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Utc).AddHours(-3);

    private static async Task Run(CurriculumPostgresTests.Pg pg, Parents parents, Publisher events, DateTime now)
    {
        await using var db = new CoachingDbContext(pg.Options);
        await new CoachingParentWeeklySummaryProcessor(db, parents, events, new Clock(now)).ProcessAsync();
    }

    private sealed class Parents(params CoachingParentSummaryRecipient[] recipients) : ICoachingParentDirectory
    {
        public Task<CoachingParentSummaryRecipientPage> GetWeeklySummaryRecipientsAsync(int pageNumber, int pageSize, CancellationToken ct) =>
            Task.FromResult(new CoachingParentSummaryRecipientPage(recipients.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(), recipients.Length));
    }

    private sealed class Publisher : ICoachingEventPublisher
    {
        public List<CoachingParentWeeklySummaryV1> Summaries { get; } = [];
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            if (message is CoachingParentWeeklySummaryV1 summary) Summaries.Add(summary);
            return Task.CompletedTask;
        }
    }

    private sealed class Clock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }
}
