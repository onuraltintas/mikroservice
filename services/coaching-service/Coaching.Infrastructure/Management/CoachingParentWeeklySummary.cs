using Coaching.Domain;
using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Contracts.Events.Coaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coaching.Infrastructure.Management;

/// <summary>
/// Roadmap Y7: from Sunday 18:00 until Monday ends (Istanbul time) each verified parent gets one summary of their child's
/// week (Monday–Sunday): planned and completed study, assignments due, sessions and the latest exam. A reminder receipt
/// keeps it to one per parent, child and week; a child with no activity at all produces no e-mail.
/// </summary>
public sealed class CoachingParentWeeklySummaryProcessor(CoachingDbContext db, ICoachingParentDirectory parents,
    ICoachingEventPublisher events, TimeProvider clock)
{
    public const string ReceiptKind = "ParentWeeklySummary";
    private const int PageSize = 200;
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public async Task<int> ProcessAsync(CancellationToken ct = default)
    {
        if (!db.Database.IsRelational()) return 0;
        var now = clock.GetUtcNow().UtcDateTime;
        var local = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, Istanbul));
        var localHour = TimeZoneInfo.ConvertTimeFromUtc(now, Istanbul).Hour;
        DateOnly weekEnd;
        if (local.DayOfWeek == DayOfWeek.Sunday && localHour >= 18) weekEnd = local;
        else if (local.DayOfWeek == DayOfWeek.Monday) weekEnd = local.AddDays(-1);
        else return 0;
        var weekStart = weekEnd.AddDays(-6);
        var rangeStart = Utc(weekStart); var rangeEnd = Utc(weekEnd.AddDays(1));
        var sent = 0;
        for (var page = 1; ; page++)
        {
            var recipients = await parents.GetWeeklySummaryRecipientsAsync(page, PageSize, ct);
            foreach (var recipient in recipients.Items)
            {
                var receipt = CoachingReminderReceipt.Create(recipient.ParentUserId, recipient.StudentUserId, ReceiptKind,
                    weekStart.ToString("yyyy-MM-dd"), rangeEnd, now);
                if (await db.CoachingReminderReceipts.AnyAsync(x => x.Id == receipt.Id, ct)) continue;
                var summary = await SummarizeAsync(receipt.Id, recipient, weekStart, weekEnd, rangeStart, rangeEnd, now, ct);
                db.CoachingReminderReceipts.Add(receipt);
                if (summary is not null) { await events.PublishAsync(summary, ct); sent++; }
                await db.SaveChangesAsync(ct);
            }
            if (recipients.Items.Count < PageSize || page * PageSize >= recipients.TotalCount) break;
        }
        return sent;
    }

    private async Task<CoachingParentWeeklySummaryV1?> SummarizeAsync(Guid id, CoachingParentSummaryRecipient recipient, DateOnly weekStart,
        DateOnly weekEnd, DateTime rangeStart, DateTime rangeEnd, DateTime now, CancellationToken ct)
    {
        var student = recipient.StudentUserId;
        var tasks = await (from task in db.StudyPlanTasks.AsNoTracking()
                           join plan in db.StudyPlanRevisions.AsNoTracking() on task.RevisionId equals plan.Id
                           where task.StudentId == student && plan.Status == StudyPlanStatus.Active && task.SkippedAt == null
                               && task.PlannedDate >= weekStart && task.PlannedDate <= weekEnd
                           select task.IsCompleted).ToListAsync(ct);
        var assignments = await (from row in db.AssignmentStudents.AsNoTracking()
                                 join assignment in db.Assignments.AsNoTracking() on row.AssignmentId equals assignment.Id
                                 where row.StudentId == student && assignment.Status != AssignmentStatus.Cancelled
                                     && assignment.DueDate >= rangeStart && assignment.DueDate < rangeEnd
                                 select new { row.Status, AssignmentStatus = assignment.Status, assignment.DueDate }).ToListAsync(ct);
        var sessions = await (from attendance in db.SessionAttendances.AsNoTracking()
                              join session in db.CoachingSessions.AsNoTracking() on attendance.SessionId equals session.Id
                              where attendance.StudentId == student && session.Status != SessionStatus.Cancelled
                                  && session.ScheduledDate >= rangeStart && session.ScheduledDate < now.AddDays(7)
                              select new { session.ScheduledDate, session.Status, attendance.AttendanceStatus }).ToListAsync(ct);
        var exam = await (from result in db.ExamResults.AsNoTracking()
                          join item in db.Exams.AsNoTracking() on result.ExamId equals item.Id
                          where result.StudentId == student && item.ExamDate >= rangeStart && item.ExamDate < rangeEnd
                          orderby item.ExamDate descending
                          select new { item.Title, result.Score }).FirstOrDefaultAsync(ct);
        var held = sessions.Where(x => x.ScheduledDate < rangeEnd && x.ScheduledDate <= now).ToArray();
        var upcoming = sessions.Count(x => x.ScheduledDate > now && x.Status == SessionStatus.Scheduled);
        if (tasks.Count == 0 && assignments.Count == 0 && held.Length == 0 && upcoming == 0 && exam is null) return null;
        return new CoachingParentWeeklySummaryV1(id, recipient.ParentUserId, recipient.ParentEmail, recipient.ParentName, student,
            recipient.StudentName, weekStart, weekEnd, tasks.Count, tasks.Count(x => x),
            assignments.Count, assignments.Count(x => AssignmentProgress.IsDone(x.Status)),
            assignments.Count(x => AssignmentProgress.IsOverdue(x.AssignmentStatus, x.Status, x.DueDate, now)),
            held.Length, held.Count(x => x.AttendanceStatus is AttendanceStatus.Present or AttendanceStatus.Late), upcoming,
            exam?.Title, exam?.Score);
    }

    private static DateTime Utc(DateOnly day) => TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), Istanbul);
}

public sealed class CoachingParentWeeklySummaryWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<CoachingParentWeeklySummaryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<CoachingParentWeeklySummaryProcessor>().ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Parent weekly summaries could not be processed; the next cycle will retry."); }
            await Task.Delay(TimeSpan.FromMinutes(30), clock, stoppingToken);
        }
    }
}
