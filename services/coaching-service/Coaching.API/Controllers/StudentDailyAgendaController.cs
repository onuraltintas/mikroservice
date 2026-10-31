using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.API.Controllers;

public sealed record StudentDailyAgendaItem(string Kind, Guid RecordId, string Title, DateOnly Date,
    int? StartMinute, int? Minutes, string TimeZoneId, Guid? PlanId = null, DateOnly? WeekStart = null, DateTime? DueAt = null);
public sealed record StudentDailyAgendaPage(string Mode, IReadOnlyList<StudentDailyAgendaItem> Items,
    int TotalCount, int PageNumber, int PageSize, int TodayCount, int UpcomingCount, int OverdueCount, DateTimeOffset GeneratedAt);

[ApiController, ApiVersion(1.0), Authorize(Roles = "Student")]
[Route("api/coaching/study-planning/daily-agenda")]
public sealed class StudentDailyAgendaController(CoachingDbContext db, ICoachingAccessPolicy access, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string mode = "today", int pageNumber = 1, CancellationToken ct = default)
    {
        if (mode is not ("today" or "upcoming" or "overdue") || pageNumber is < 1 or > 1000)
            return BadRequest(new { message = "Geçerli bir günlük iş filtresi ve sayfa seçin." });
        if (access.CurrentUserId is not { } student || !access.IsCurrentStudent(student)
            || !await db.CoachingStudentProfiles.AsNoTracking().AnyAsync(x => x.UserId == student && x.IsActive, ct))
            return StatusCode(403, new { message = "Öğrenci çalışma alanına erişiminiz yok." });

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
            var result = await StudentDailyAgendaQuery.ReadAsync(db, student, mode, pageNumber, clock.GetUtcNow(), ct);
            await tx.CommitAsync(ct);
            return (IActionResult)Ok(result);
        });
    }

}

/// <summary>
/// The student's daily agenda (study tasks, assignments, weekly decisions) for one mode, in the student's own time zone.
/// Shared by the agenda endpoint and the T3 "Bugün" summary so both count and order work the same way.
/// The caller runs it inside one RepeatableRead transaction.
/// </summary>
public static class StudentDailyAgendaQuery
{
    public static async Task<StudentDailyAgendaPage> ReadAsync(CoachingDbContext db, Guid student, string mode, int pageNumber,
        DateTimeOffset now, CancellationToken ct)
    {
        var utcNow = now.UtcDateTime;
        var plan = await db.StudyPlanRevisions.AsNoTracking().Where(x => x.StudentId == student && x.Status == StudyPlanStatus.Active)
            .Select(x => new { x.Id, x.WeeklySource }).SingleOrDefaultAsync(ct);
        // Roadmap D2: one "today" for study, assignments and decisions, in the student's own time zone.
        var zone = await Coaching.Infrastructure.StudyPlanning.CoachingStudentZone.IdAsync(db, student, ct);
        var studentZone = TimeZoneInfo.FindSystemTimeZoneById(zone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, studentZone).DateTime);
        var tomorrowUtc = TimeZoneInfo.ConvertTimeToUtc(today.AddDays(1).ToDateTime(TimeOnly.MinValue), studentZone);
        var horizonUtc = TimeZoneInfo.ConvertTimeToUtc(today.AddDays(8).ToDateTime(TimeOnly.MinValue), studentZone);
        var local = TimeZoneInfo.ConvertTime(now, studentZone);
        var studyToday = DateOnly.FromDateTime(local.DateTime); var studyEnd = studyToday.AddDays(7);
        var minute = local.Hour * 60 + local.Minute; var planId = plan?.Id ?? Guid.Empty;

        var studies = db.StudyPlanTasks.AsNoTracking().Where(x => x.StudentId == student && x.RevisionId == planId
                && !x.IsCompleted && x.SkippedAt == null && x.PlannedDate <= studyEnd)
            .Select(x => new { Row = x, Mode = x.PlannedDate < studyToday || x.PlannedDate == studyToday
                && x.PlannedStartMinute.HasValue && x.PlannedStartMinute + x.PlannedMinutes <= minute
                ? "overdue" : x.PlannedDate == studyToday ? "today" : "upcoming" });
        var assignments = db.AssignmentStudents.AsNoTracking().Where(x => x.StudentId == student && x.Assignment.Status == AssignmentStatus.Active
                && x.SubmittedAt == null && (x.Status == StudentAssignmentStatus.Assigned || x.Status == StudentAssignmentStatus.InProgress)
                && x.Assignment.DueDate < horizonUtc)
            .Select(x => new { Row = x, Mode = x.Assignment.DueDate < utcNow ? "overdue" : x.Assignment.DueDate < tomorrowUtc ? "today" : "upcoming" });
        var decisions = db.Set<WeeklyDecision>().AsNoTracking().Where(x => x.StudentId == student
                && (x.Status == "Planned" || x.Status == "InProgress") && x.DueOn <= today.AddDays(7))
            .Select(x => new { Row = x, Mode = x.DueOn < today ? "overdue" : x.DueOn == today ? "today" : "upcoming" });

        var studyCounts = await studies.GroupBy(x => x.Mode).Select(x => new { Mode = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Mode, x => x.Count, ct);
        var assignmentCounts = await assignments.GroupBy(x => x.Mode).Select(x => new { Mode = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Mode, x => x.Count, ct);
        var decisionCounts = await decisions.GroupBy(x => x.Mode).Select(x => new { Mode = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Mode, x => x.Count, ct);
        int Count(string key) => studyCounts.GetValueOrDefault(key) + assignmentCounts.GetValueOrDefault(key) + decisionCounts.GetValueOrDefault(key);
        const int pageSize = 20; var total = Count(mode);
        var page = Math.Min(pageNumber, Math.Max(1, (total + pageSize - 1) / pageSize)); var take = page * pageSize;
        // Only the leading candidates for the requested page are read from each source, never the whole yearly plan.
        var items = new List<StudentDailyAgendaItem>();
        items.AddRange(await studies.Where(x => x.Mode == mode).OrderBy(x => x.Row.PlannedDate).ThenBy(x => x.Row.PlannedStartMinute ?? 1439).ThenBy(x => x.Row.Id)
            .Take(take).Select(x => new StudentDailyAgendaItem("study", x.Row.Id, x.Row.Title, x.Row.PlannedDate,
                x.Row.PlannedStartMinute, x.Row.PlannedMinutes, zone, planId, null, null)).ToArrayAsync(ct));
        var dueRows = await assignments.Where(x => x.Mode == mode).OrderBy(x => x.Row.Assignment.DueDate).ThenBy(x => x.Row.AssignmentId).Take(take)
            .Select(x => new { x.Row.AssignmentId, x.Row.Assignment.Title, x.Row.Assignment.DueDate, x.Row.Assignment.EstimatedDurationMinutes }).ToArrayAsync(ct);
        foreach (var row in dueRows)
        {
            var due = TimeZoneInfo.ConvertTimeFromUtc(row.DueDate, studentZone);
            items.Add(new("assignment", row.AssignmentId, row.Title, DateOnly.FromDateTime(due), due.Hour * 60 + due.Minute,
                row.EstimatedDurationMinutes, zone, DueAt: row.DueDate));
        }
        items.AddRange(await decisions.Where(x => x.Mode == mode).OrderBy(x => x.Row.DueOn).ThenBy(x => x.Row.Id).Take(take)
            .Select(x => new StudentDailyAgendaItem("decision", x.Row.Id, x.Row.Title, x.Row.DueOn, null, null,
                zone, null, x.Row.ReviewWeekStart, null)).ToArrayAsync(ct));
        var rows = items.OrderBy(SortTime).ThenBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.RecordId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        return new StudentDailyAgendaPage(mode, rows, total, page, pageSize, Count("today"), Count("upcoming"), Count("overdue"), now);
    }

    private static DateTime SortTime(StudentDailyAgendaItem item) => item.DueAt ?? TimeZoneInfo.ConvertTimeToUtc(
        item.Date.ToDateTime(TimeOnly.MinValue).AddMinutes(item.StartMinute ?? 1439), TimeZoneInfo.FindSystemTimeZoneById(item.TimeZoneId));
}
