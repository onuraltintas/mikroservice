using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Contracts.Events.Coaching;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Coaching.Infrastructure.Reminders;

public sealed class CoachingReminderProcessor(CoachingDbContext db, ICoachingEventPublisher events, TimeProvider clock)
{
    private const int BatchSize = 100;

    public async Task<int> ProcessAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var studyCount = await LockedAsync(() => StudyDaysAsync(now, ct), false, ct);
        return studyCount + await LockedAsync(() => SessionsAsync(now, ct), true, ct);
    }

    private async Task<int> StudyDaysAsync(DateTimeOffset now, CancellationToken ct)
    {
        var count = 0;
        var zones = (await db.StudyAvailability.AsNoTracking().Select(x => x.TimeZoneId).Distinct().ToListAsync(ct))
            .Append("Europe/Istanbul").Distinct().OrderBy(x => x, StringComparer.Ordinal);
        foreach (var zoneId in zones)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId); var local = TimeZoneInfo.ConvertTime(now, zone);
            if (local.Hour < 9) continue;
            var date = DateOnly.FromDateTime(local.DateTime);
            var occurrence = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var due = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(9, 0)), zone);
            var plans = await (from plan in db.StudyPlanRevisions.AsNoTracking()
                join hours in db.StudyAvailability.AsNoTracking() on plan.StudentId equals hours.StudentId into preferences
                from hours in preferences.DefaultIfEmpty()
                where plan.IsActive && (hours == null ? zoneId == "Europe/Istanbul" : hours.TimeZoneId == zoneId)
                    && db.StudyPlanTasks.Any(task => task.RevisionId == plan.Id && task.StudentId == plan.StudentId
                        && !task.IsCompleted && task.PlannedDate == date)
                    && !db.CoachingReminderReceipts.Any(receipt => receipt.Kind == "StudyDay" && receipt.SourceId == plan.Id
                        && receipt.RecipientUserId == plan.StudentId && receipt.OccurrenceKey == occurrence)
                orderby plan.StudentId, plan.Id
                select new { plan.Id, plan.StudentId })
                .Take(BatchSize - count).ToListAsync(ct);
            foreach (var plan in plans)
            {
                var key = "coaching-study-plan:" + plan.StudentId;
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
                if (!await db.StudyPlanRevisions.AnyAsync(x => x.Id == plan.Id && x.IsActive, ct)
                    || !await db.StudyPlanTasks.AnyAsync(x => x.RevisionId == plan.Id && x.StudentId == plan.StudentId && x.PlannedDate == date && !x.IsCompleted, ct)) continue;
                if (await QueueAsync(CoachingReminderReceipt.Create(plan.StudentId, plan.Id, "StudyDay", occurrence, due, now.UtcDateTime), ct)) count++;
                if (count == BatchSize) break;
            }
            if (count == BatchSize) break;
        }
        return count;
    }

    private async Task<int> SessionsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var start = now.UtcDateTime; var end = start.AddHours(1); var count = 0;
        var sessions = await db.CoachingSessions.AsNoTracking().Include(x => x.Attendances)
            .Where(x => x.Status == SessionStatus.Scheduled && x.ScheduledDate > start && x.ScheduledDate <= end
                && (!db.CoachingReminderReceipts.Any(receipt => receipt.Kind == "Session" && receipt.SourceId == x.Id
                        && receipt.RecipientUserId == x.TeacherId && receipt.DueAt == x.ScheduledDate)
                    || x.Attendances.Any(attendance => !db.CoachingReminderReceipts.Any(receipt => receipt.Kind == "Session"
                        && receipt.SourceId == x.Id && receipt.RecipientUserId == attendance.StudentId && receipt.DueAt == x.ScheduledDate))))
            .OrderBy(x => x.ScheduledDate).ThenBy(x => x.Id).Take(BatchSize).ToListAsync(ct);
        foreach (var session in sessions)
        {
            var existing = (await db.CoachingReminderReceipts.AsNoTracking()
                .Where(x => x.Kind == "Session" && x.SourceId == session.Id && x.DueAt == session.ScheduledDate)
                .Select(x => x.RecipientUserId).ToListAsync(ct)).ToHashSet();
            foreach (var recipient in session.Attendances.Select(x => x.StudentId).Append(session.TeacherId).Distinct())
            {
                if (existing.Contains(recipient)) continue;
                if (await QueueAsync(CoachingReminderReceipt.Create(recipient, session.Id, "Session",
                    session.ScheduledDate.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture), session.ScheduledDate, now.UtcDateTime), ct)) count++;
                if (count == BatchSize) break;
            }
            if (count == BatchSize) break;
        }
        return count;
    }

    private async Task<bool> QueueAsync(CoachingReminderReceipt receipt, CancellationToken ct)
    {
        if (db.CoachingReminderReceipts.Local.Any(x => x.Id == receipt.Id)
            || await db.CoachingReminderReceipts.AnyAsync(x => x.Id == receipt.Id, ct)) return false;
        db.CoachingReminderReceipts.Add(receipt);
        await events.PublishAsync(new CoachingReminderDueEvent(receipt.Id, receipt.RecipientUserId, receipt.SourceId, receipt.Kind, receipt.DueAt), ct);
        return true;
    }

    private async Task<int> LockedAsync(Func<Task<int>> action, bool sessions, CancellationToken ct)
    {
        var ready = false; var completed = 0; Guid[] receiptIds = [];
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<object?, int>(null, async (_, _, token) =>
        {
            ready = false;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var committed = false;
            var priorStates = db.ChangeTracker.Entries<OutboxState>().ToDictionary(x => x.Entity.OutboxId, x => x.State);
            try
            {
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtextextended('coaching-reminders', 0))", ct);
                if (sessions) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtextextended('coaching-session-scheduling', 0))", ct);
                completed = await action();
                receiptIds = db.ChangeTracker.Entries<CoachingReminderReceipt>().Where(x => x.State == EntityState.Added).Select(x => x.Entity.Id).ToArray();
                await db.SaveChangesAsync(ct); ready = true;
                await transaction.CommitAsync(ct); committed = true; return completed;
            }
            finally
            {
                foreach (var entry in db.ChangeTracker.Entries<CoachingReminderReceipt>().ToArray()) entry.State = EntityState.Detached;
                if (!committed)
                {
                    foreach (var entry in db.ChangeTracker.Entries<OutboxMessage>().ToArray()) entry.State = EntityState.Detached;
                    foreach (var entry in db.ChangeTracker.Entries<OutboxState>().ToArray()) entry.State = priorStates.GetValueOrDefault(entry.Entity.OutboxId, EntityState.Added);
                }
            }
        }, async (_, _, token) =>
        {
            var committed = ready && receiptIds.Length > 0 && await db.CoachingReminderReceipts.AsNoTracking().CountAsync(x => Enumerable.Contains(receiptIds, x.Id), token) == receiptIds.Length;
            if (committed)
            {
                var states = db.ChangeTracker.Entries<OutboxState>().Where(x => x.State == EntityState.Added).ToArray(); var ids = states.Select(x => x.Entity.OutboxId).ToArray();
                var persisted = await db.Set<OutboxState>().AsNoTracking().Where(x => Enumerable.Contains(ids, x.OutboxId)).Select(x => x.OutboxId).ToListAsync(token);
                foreach (var state in states.Where(x => persisted.Contains(x.Entity.OutboxId))) state.State = EntityState.Unchanged;
            }
            return new ExecutionResult<int>(committed, completed);
        }, ct);
    }
}
