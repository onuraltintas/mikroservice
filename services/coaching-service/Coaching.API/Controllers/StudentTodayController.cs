using Coaching.Application.Authorization;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.API.Controllers;

public sealed record StudentTodayNextStep(StudentDailyAgendaItem Item, bool Overdue);
public sealed record StudentTodayView(DateOnly Today, string TimeZoneId, DateTimeOffset GeneratedAt, StudentTodayNextStep? NextStep,
    IReadOnlyList<StudentDailyAgendaItem> Items, IReadOnlyList<StudentDailyAgendaItem> Overdue,
    int TodayCount, int OverdueCount, int UpcomingCount, StudentTodayExam? Exam, StudentTodayStreak Streak, StudentTodayWeek Week,
    StudentTodayGoal? Goal, StudentTodayReview WeeklyReview, StudentTodaySession? NextSession, bool HasActivePlan);

/// <summary>
/// T3 (31.10.2026): one call for the student's "Bugün" screen. The agenda part uses the same query as the daily agenda
/// (today and overdue, first page each); the next step is the oldest overdue assignment, else the first item of today,
/// else the oldest other overdue item.
/// </summary>
[ApiController, ApiVersion(1.0), Authorize(Roles = "Student")]
[Route("api/coaching/students/me/today")]
public sealed class StudentTodayController(CoachingDbContext db, ICoachingAccessPolicy access, CoachingStudentTodayService today, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        if (access.CurrentUserId is not { } student || !access.IsCurrentStudent(student)
            || !await db.CoachingStudentProfiles.AsNoTracking().AnyAsync(x => x.UserId == student && x.IsActive, ct))
            return StatusCode(403, new { message = "Öğrenci çalışma alanına erişiminiz yok." });

        var now = clock.GetUtcNow();
        var (todayPage, overduePage) = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
            var current = await StudentDailyAgendaQuery.ReadAsync(db, student, "today", 1, now, ct);
            var late = await StudentDailyAgendaQuery.ReadAsync(db, student, "overdue", 1, now, ct);
            await tx.CommitAsync(ct);
            return (current, late);
        });
        var facts = await today.ReadAsync(student, ct);
        var next = overduePage.Items.FirstOrDefault(x => x.Kind == "assignment") is { } lateWork ? new StudentTodayNextStep(lateWork, true)
            : todayPage.Items.FirstOrDefault() is { } first ? new StudentTodayNextStep(first, false)
            : overduePage.Items.FirstOrDefault() is { } other ? new StudentTodayNextStep(other, true) : null;
        return Ok(new StudentTodayView(facts.Today, facts.TimeZoneId, now, next, todayPage.Items, overduePage.Items,
            todayPage.TodayCount, todayPage.OverdueCount, todayPage.UpcomingCount, facts.Exam, facts.Streak, facts.Week,
            facts.Goal, facts.WeeklyReview, facts.NextSession, facts.HasActivePlan));
    }
}
