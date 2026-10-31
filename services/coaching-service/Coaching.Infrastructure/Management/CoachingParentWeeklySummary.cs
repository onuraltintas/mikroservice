using Coaching.Domain;
using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Events.Coaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coaching.Infrastructure.Management;

/// <summary>
/// Roadmap Y7: from Sunday 18:00 until Monday ends (in the student's own time zone, CoachingStudentZone) each verified parent
/// gets one summary of their child's week (Monday–Sunday). A reminder receipt keeps it to one per parent, child and week.
/// V3 (parent panel): one e-mail with two optional sections. Koçluk (planned and completed study, assignments due, sessions,
/// latest exam) only when the child uses Coaching, left it visible to the parent and has something for the week; Hızlı okuma
/// from the speed-reading service only when the child uses speed reading and left it visible. No section, no e-mail.
/// </summary>
public sealed class CoachingParentWeeklySummaryProcessor(CoachingDbContext db, ICoachingParentDirectory parents,
    ICoachingEventPublisher events, TimeProvider clock, ISpeedReadingParentSummaryClient? speedReading = null,
    ILogger<CoachingParentWeeklySummaryProcessor>? logger = null)
{
    public const string ReceiptKind = "ParentWeeklySummary";
    private const int PageSize = 200;

    public async Task<int> ProcessAsync(CancellationToken ct = default)
    {
        if (!db.Database.IsRelational()) return 0;
        var now = clock.GetUtcNow().UtcDateTime;
        var sent = 0;
        // Coaching audit: Identity is read one page at a time; only the current page is held in memory.
        for (var page = 1; ; page++)
        {
            var recipients = await parents.GetWeeklySummaryRecipientsAsync(page, PageSize, ct);
            var students = recipients.Items.Select(x => x.StudentUserId).Distinct().ToArray();
            // Coaching audit K3/K4: the week, the Sunday-evening send and "today" follow the student's own time zone.
            var zones = await CoachingStudentZone.IdsAsync(db, students, ct);
            foreach (var recipient in recipients.Items)
            {
                if (!recipient.WantsCoaching && !recipient.WantsSpeedReading) continue;
                var zone = CoachingStudentZone.Find(zones.GetValueOrDefault(recipient.StudentUserId));
                var local = TimeZoneInfo.ConvertTimeFromUtc(now, zone);
                var today = DateOnly.FromDateTime(local);
                DateOnly weekEnd;
                if (today.DayOfWeek == DayOfWeek.Sunday && local.Hour >= 18) weekEnd = today;
                else if (today.DayOfWeek == DayOfWeek.Monday) weekEnd = today.AddDays(-1);
                else continue;
                var weekStart = weekEnd.AddDays(-6);
                var week = new Week(weekStart, weekEnd, Utc(weekStart, zone), Utc(weekEnd.AddDays(1), zone), zone);
                var receipt = CoachingReminderReceipt.Create(recipient.ParentUserId, recipient.StudentUserId, ReceiptKind,
                    weekStart.ToString("yyyy-MM-dd"), week.RangeEnd, now);
                if (await db.CoachingReminderReceipts.AnyAsync(x => x.Id == receipt.Id, ct)) continue;
                // Read before the receipt's transaction; an outage leaves the week unused so the next cycle tries again.
                // The speed-reading service takes the same Monday (the student's local week start).
                SpeedReadingParentWeek? reading = null;
                if (recipient.WantsSpeedReading && speedReading is not null)
                {
                    try { reading = await speedReading.GetWeekAsync(recipient.StudentUserId, weekStart, ct); }
                    catch (Exception error) when (!ct.IsCancellationRequested && error is HttpRequestException or TaskCanceledException
                        or InvalidOperationException or System.Text.Json.JsonException)
                    {
                        logger?.LogWarning(error, "A student's speed-reading week could not be read; the parent summary waits for the next cycle.");
                        continue;
                    }
                }
                if (await SendOnceAsync(receipt, recipient, week, reading, now, ct)) sent++;
            }
            if (recipients.Items.Count < PageSize || page * PageSize >= recipients.TotalCount) break;
        }
        return sent;
    }

    private sealed record Week(DateOnly Start, DateOnly End, DateTime RangeStart, DateTime RangeEnd, TimeZoneInfo Zone);

    /// <summary>
    /// Coaching audit K3: one summary per parent, child and week even when several instances run at once. The receipt is
    /// checked again under a lock for that receipt, and receipt and event are committed together.
    /// </summary>
    private async Task<bool> SendOnceAsync(CoachingReminderReceipt receipt, CoachingParentSummaryRecipient recipient, Week week,
        SpeedReadingParentWeek? reading, DateTime now, CancellationToken ct)
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
                var coaching = recipient.WantsCoaching ? await SummarizeCoachingAsync(recipient.StudentUserId, week, now, ct) : null;
                var section = reading is null ? null : new CoachingParentWeeklySpeedReadingSection(reading.Minutes, reading.ActiveDays,
                    reading.ExercisesCompleted, reading.LatestWpm, reading.WpmChange, reading.AverageComprehension, reading.ProgramDay,
                    reading.ProgramTotalDays, reading.BadgesEarnedThisWeek);
                db.CoachingReminderReceipts.Add(receipt);
                var publish = coaching is not null || section is not null;
                if (publish)
                    await events.PublishAsync(new CoachingParentWeeklySummaryV2(receipt.Id, recipient.ParentUserId, recipient.ParentEmail,
                        recipient.ParentName, recipient.StudentUserId, recipient.StudentName, week.Start, week.End, coaching, section), ct);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct); committed = true;
                return publish;
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

    private async Task<CoachingParentWeeklyCoachingSection?> SummarizeCoachingAsync(Guid student, Week week, DateTime now, CancellationToken ct)
    {
        var (weekStart, weekEnd, rangeStart, rangeEnd) = (week.Start, week.End, week.RangeStart, week.RangeEnd);
        var tasks = await (from task in db.StudyPlanTasks.AsNoTracking()
                           join plan in db.StudyPlanRevisions.AsNoTracking() on task.RevisionId equals plan.Id
                           where task.StudentId == student && plan.Status == StudyPlanStatus.Active && task.SkippedAt == null
                               && task.PlannedDate >= weekStart && task.PlannedDate <= weekEnd
                           select new { task.IsCompleted, task.PlannedDate, task.PlannedStartMinute }).ToListAsync(ct);
        // K4 (31.10.2026): the e-mail can leave on Sunday evening; an unfinished task of that day that has not started yet (or
        // has no time) is still ahead, so it is neither counted as planned-and-missed nor shown as left undone. "Now" is the
        // student's own (coaching audit: this used to be Istanbul for everyone).
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, week.Zone);
        var today = DateOnly.FromDateTime(localNow);
        var minute = localNow.Hour * 60 + localNow.Minute;
        var counted = tasks.Where(x => x.IsCompleted || x.PlannedDate < today
            || x.PlannedDate == today && x.PlannedStartMinute is { } start && start <= minute).Select(x => x.IsCompleted).ToList();
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
        if (counted.Count == 0 && assignments.Count == 0 && held.Length == 0 && upcoming == 0 && exam is null) return null;
        return new CoachingParentWeeklyCoachingSection(counted.Count, counted.Count(x => x),
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
