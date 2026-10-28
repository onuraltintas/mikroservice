using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Roadmap D5: a daily study streak (at least one plan task completed or an assignment submitted that day),
// badges and a celebration when a goal is reached. Student, their parents and teachers see it. No AI.
public sealed class CoachingMotivationTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class Readers(params Guid[] allowed) : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? requestedInstitutionId, bool isSystemAdministrator, CancellationToken ct) => Task.FromResult<Guid?>(null);
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(allowed.Contains(viewerUserId) ? studentIds.ToArray() : []);
    }

    // 2026-11-10 12:00 Istanbul.
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
    private static DateTime Istanbul(int day, int hour) => new DateTime(2026, 11, day, hour, 0, 0, DateTimeKind.Utc).AddHours(-3);

    [Fact]
    public async Task StreakCountsDaysWithCompletedTasksOrSubmissionsInTheStudentsDay()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var student = Guid.NewGuid(); var teacher = Guid.NewGuid(); var parent = Guid.NewGuid();
        var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Plan"); plan.Activate(); db.Add(plan);
        void Task(int day, int hour)
        {
            var task = StudyPlanTask.Create(plan, new DateOnly(2026, 11, day), "Görev", 30); task.Complete(30); db.Add(task);
            db.Entry(task).Property(x => x.CompletedAt).CurrentValue = Istanbul(day, hour);
        }
        // An older run of three days, a gap, then 7, 8 and 9 November; 1 November late at night still counts as the 1st.
        Task(1, 23); Task(2, 10); Task(3, 10); Task(7, 9); Task(8, 20);
        db.Add(StudyPlanTask.Create(plan, new DateOnly(2026, 11, 9), "Yapılmadı", 30));
        var assignment = Assignment.Create(teacher, "Paragraf", Istanbul(12, 18)); assignment.AssignToStudents([student]); assignment.SubmitAssignment(student); db.Add(assignment);
        await db.SaveChangesAsync();
        db.Entry(assignment.AssignedStudents.Single()).Property(x => x.SubmittedAt).CurrentValue = Istanbul(9, 21); await db.SaveChangesAsync();
        var goal = AcademicGoal.Create(student, "LGS 450", GoalCategory.ExamPreparation, null, null); goal.UpdateProgress(100); db.Add(goal);
        await db.SaveChangesAsync();
        db.Entry(goal).Property(x => x.CompletedAt).CurrentValue = Istanbul(8, 12); await db.SaveChangesAsync();

        var service = new CoachingMotivationService(db, new Readers(teacher, parent), new Clock(Now));
        var mine = await service.GetAsync(student, student, default);
        Assert.Equal((3, 3, false), (mine.CurrentStreak, mine.LongestStreak, mine.ActiveToday));
        Assert.Equal(new DateOnly(2026, 11, 9), mine.LastActiveDay);
        DateOnly? Earned(string code) => mine.Badges.Single(x => x.Code == code).EarnedOn;
        Assert.Equal(new DateOnly(2026, 11, 1), Earned("first-day"));
        Assert.Equal(new DateOnly(2026, 11, 3), Earned("streak-3"));
        Assert.Null(Earned("streak-7"));
        Assert.Equal(new DateOnly(2026, 11, 9), Earned("first-submission"));
        Assert.Equal(new DateOnly(2026, 11, 8), Earned("goal-reached"));
        Assert.Equal(("LGS 450", goal.Id), (mine.Celebration!.GoalTitle, mine.Celebration.GoalId));

        Assert.Equal(3, (await service.GetAsync(parent, student, default)).CurrentStreak);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetAsync(Guid.NewGuid(), student, default));

        // A week later the celebration is over and the streak has lapsed.
        var later = new CoachingMotivationService(db, new Readers(), new Clock(Now.AddDays(8)));
        var after = await later.GetAsync(student, student, default);
        Assert.Equal((0, 3), (after.CurrentStreak, after.LongestStreak)); Assert.Null(after.Celebration);
    }
}
