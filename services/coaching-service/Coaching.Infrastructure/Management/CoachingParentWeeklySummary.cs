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
/// Roadmap Y7: from Sunday 18:00 until Monday ends (in the student's time zone; Istanbul by default) each verified parent gets one summary of their child's
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
        var sent = 0;
        for (var page = 1; ; page++)
        {
            var recipients = await parents.GetWeeklySummaryRecipientsAsync(page, PageSize, ct);
            var students = recipients.Items.Select(x => x.StudentUserId).Distinct().ToArray();
            // Coaching audit K3: the week and the Sunday-evening send follow the student's own time zone, as the study
            // reminders do; students without study hours use Istanbul.
            var zones = await db.StudyAvailability.AsNoTracking().Where(x => Enumerable.Contains(students, x.StudentId))
                .ToDictionaryAsync(x => x.StudentId, x => x.TimeZoneId, ct);
            foreach (var recipient in recipients.Items)
            {
                var zone = Zone(zones.GetValueOrDefault(recipient.StudentUserId));
                var local = TimeZoneInfo.ConvertTimeFromUtc(now, zone);
                var today = DateOnly.FromDateTime(local);
                DateOnly weekEnd;
                if (today.DayOfWeek == DayOfWeek.Sunday && local.Hour >= 18) weekEnd = today;
                else if (today.DayOfWeek == DayOfWeek.Monday) weekEnd = today.AddDays(-1);
                else continue;
                var weekStart = weekEnd.AddDays(-6);
                var rangeStart = Utc(weekStart, zone); var rangeEnd = Utc(weekEnd.AddDays(1), zone);
                var receipt = CoachingReminderReceipt.Create(recipient.ParentUserId, recipient.StudentUserId, ReceiptKind,
                    weekStart.ToString("yyyy-MM-dd"), rangeEnd, now);
                if (await db.CoachingReminderReceipts.AnyAsync(x => x.Id == receipt.Id, ct)) continue;
                if (await SendOnceAsync(receipt, recipient, weekStart, weekEnd, rangeStart, rangeEnd, now, ct)) sent++;
            }
            if (recipients.Items.Count < PageSize || page * PageSize >= recipients.TotalCount) break;
        }
        return sent;
    }

    /// <summary>
    /// Coaching audit K3: one summary per parent, child and week even when several instances run at once. The receipt is
    /// checked again under a lock for that receipt, and receipt and event are committed together.
    /// </summary>
    private async Task<bool> SendOnceAsync(CoachingReminderReceipt receipt, CoachingParentSummaryRecipient recipient, DateOnly weekStart,
        DateOnly weekEnd, DateTime rangeStart, DateTime rangeEnd, DateTime now, CancellationToken ct)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var committed = false;
            try
            {
                var key = $"coaching-parent-summary:{receipt.Id:N}";
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
                if (await db.CoachingReminderReceipts.AnyAsync(x => x.Id == receipt.Id, ct)) return false;
                var summary = await SummarizeAsync(receipt.Id, recipient, weekStart, weekEnd, rangeStart, rangeEnd, now, ct);
                db.CoachingReminderReceipts.Add(receipt);
                if (summary is not null) await events.PublishAsync(summary, ct);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct); committed = true;
                return summary is not null;
            }
            finally
            {
                foreach (var entry in db.ChangeTracker.Entries<CoachingReminderReceipt>().ToArray()) entry.State = EntityState.Detached;
                if (!committed)
                    foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is MassTransit.EntityFrameworkCoreIntegration.OutboxMessage).ToArray())
                        entry.State = EntityState.Detached;
            }
        });
    }

    private static TimeZoneInfo Zone(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { } catch (InvalidTimeZoneException) { }
        return Istanbul;
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

    private static DateTime Utc(DateOnly day, TimeZoneInfo zone) => TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), zone);
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
