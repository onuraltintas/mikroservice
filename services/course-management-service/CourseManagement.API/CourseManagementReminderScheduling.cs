using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

internal static class CourseManagementReminderScheduling
{
    public static Task ScheduleLessonAsync(CourseManagementDbContext db, Guid workspaceId, Guid lessonId,
        DateTimeOffset startsAt, IReadOnlyCollection<Guid> recipientUserIds, DateTimeOffset now, CancellationToken ct)
        => SynchronizeAsync(db, workspaceId, lessonId, CourseManagementReminderType.Lesson, startsAt,
            CourseManagementReminderSchedule.LessonNotificationTime(startsAt), recipientUserIds, now, ct);

    public static Task ScheduleAssignmentAsync(CourseManagementDbContext db, Guid workspaceId, Guid assignmentId,
        DateTimeOffset dueAt, IReadOnlyCollection<Guid> recipientUserIds, DateTimeOffset now, CancellationToken ct)
        => SynchronizeAsync(db, workspaceId, assignmentId, CourseManagementReminderType.Assignment, dueAt,
            CourseManagementReminderSchedule.AssignmentNotificationTime(dueAt), recipientUserIds, now, ct);

    public static async Task ScheduleInstallmentAsync(CourseManagementDbContext db, Installment installment,
        Guid? recipientUserId, DateTimeOffset now, CancellationToken ct)
    {
        if (recipientUserId is not Guid userId || userId == Guid.Empty) return;
        var times = CourseManagementReminderSchedule.InstallmentTimes(installment.DueOn);
        Guid[] recipients = [userId];
        await SynchronizeAsync(db, installment.WorkspaceId, installment.Id, CourseManagementReminderType.InstallmentUpcoming,
            times.DueAt, times.UpcomingAt, recipients, now, ct);
        await SynchronizeAsync(db, installment.WorkspaceId, installment.Id, CourseManagementReminderType.InstallmentDue,
            times.DueAt, times.DueAt, recipients, now, ct);
    }

    public static async Task ScheduleExistingForStudentAsync(CourseManagementDbContext db, Guid workspaceId,
        Guid studentId, Guid recipientUserId, DateTimeOffset now, CancellationToken ct)
    {
        var lessons = await (from participant in db.LessonParticipants.AsNoTracking()
            join lesson in db.LessonSessions.AsNoTracking() on participant.LessonSessionId equals lesson.Id
            where participant.WorkspaceId == workspaceId && participant.StudentId == studentId && participant.IsActive
                && lesson.WorkspaceId == workspaceId && lesson.Status == LessonSessionStatus.Scheduled && lesson.StartsAt > now
            select new { lesson.Id, lesson.StartsAt }).ToListAsync(ct);
        foreach (var lesson in lessons)
            await EnsureAsync(db, workspaceId, lesson.Id, CourseManagementReminderType.Lesson, lesson.StartsAt,
                CourseManagementReminderSchedule.LessonNotificationTime(lesson.StartsAt), recipientUserId, now, ct);

        var assignments = await (from recipient in db.AssignmentRecipients.AsNoTracking()
            join assignment in db.Assignments.AsNoTracking() on recipient.AssignmentId equals assignment.Id
            where recipient.WorkspaceId == workspaceId && recipient.StudentId == studentId
                && assignment.WorkspaceId == workspaceId && assignment.DueAt > now
            select new { assignment.Id, assignment.DueAt }).ToListAsync(ct);
        foreach (var assignment in assignments)
            await EnsureAsync(db, workspaceId, assignment.Id, CourseManagementReminderType.Assignment, assignment.DueAt,
                CourseManagementReminderSchedule.AssignmentNotificationTime(assignment.DueAt), recipientUserId, now, ct);

        var installments = await db.Installments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
            && item.StudentId == studentId && item.Amount > 0 && item.ReversalOfInstallmentId == null).ToListAsync(ct);
        var installmentIds = installments.Select(item => item.Id).ToArray();
        var reversedIds = await db.Installments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
            && item.ReversalOfInstallmentId != null && Enumerable.Contains(installmentIds, item.ReversalOfInstallmentId.Value))
            .Select(item => item.ReversalOfInstallmentId!.Value).ToListAsync(ct);
        var allocations = await db.PaymentAllocations.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
            && Enumerable.Contains(installmentIds, item.InstallmentId)).ToListAsync(ct);
        foreach (var installment in installments.Where(item => !reversedIds.Contains(item.Id)
            && item.Amount - allocations.Where(allocation => allocation.InstallmentId == item.Id).Sum(allocation => allocation.Amount) > 0))
        {
            var times = CourseManagementReminderSchedule.InstallmentTimes(installment.DueOn);
            await EnsureAsync(db, workspaceId, installment.Id, CourseManagementReminderType.InstallmentUpcoming,
                times.DueAt, times.UpcomingAt, recipientUserId, now, ct);
            await EnsureAsync(db, workspaceId, installment.Id, CourseManagementReminderType.InstallmentDue,
                times.DueAt, times.DueAt, recipientUserId, now, ct);
        }
    }

    public static async Task CancelEntityAsync(CourseManagementDbContext db, Guid workspaceId, Guid relatedEntityId,
        CancellationToken ct, Guid? recipientUserId = null)
    {
        var reminders = await db.Reminders.Where(item => item.WorkspaceId == workspaceId
            && item.RelatedEntityId == relatedEntityId && item.Status == CourseManagementReminderStatus.Pending
            && (recipientUserId == null || item.RecipientUserId == recipientUserId)).ToListAsync(ct);
        foreach (var reminder in reminders) reminder.Cancel();
    }

    public static async Task SynchronizeAsync(CourseManagementDbContext db, Guid workspaceId, Guid relatedEntityId,
        CourseManagementReminderType type, DateTimeOffset targetAt, DateTimeOffset notifyAt,
        IReadOnlyCollection<Guid> recipientUserIds, DateTimeOffset now, CancellationToken ct)
    {
        var targetUtc = targetAt.ToUniversalTime();
        var recipients = recipientUserIds.Where(id => id != Guid.Empty).Distinct().ToHashSet();
        var existing = await db.Reminders.Where(item => item.WorkspaceId == workspaceId
            && item.RelatedEntityId == relatedEntityId && item.Type == type).ToListAsync(ct);

        foreach (var reminder in existing.Where(item => item.Status == CourseManagementReminderStatus.Pending
            && (item.TargetAt != targetUtc || !recipients.Contains(item.RecipientUserId))))
            reminder.Cancel();

        if (notifyAt <= now) return;

        foreach (var recipientId in recipients)
            await EnsureAsync(db, workspaceId, relatedEntityId, type, targetAt, notifyAt, recipientId, now, ct, existing);
    }

    private static async Task EnsureAsync(CourseManagementDbContext db, Guid workspaceId, Guid relatedEntityId,
        CourseManagementReminderType type, DateTimeOffset targetAt, DateTimeOffset notifyAt, Guid recipientId,
        DateTimeOffset now, CancellationToken ct, IReadOnlyCollection<CourseManagementReminder>? knownRows = null)
    {
        if (recipientId == Guid.Empty || notifyAt <= now) return;
        var targetUtc = targetAt.ToUniversalTime();
        var existing = knownRows ?? await db.Reminders.Where(item => item.WorkspaceId == workspaceId
            && item.RelatedEntityId == relatedEntityId && item.Type == type && item.RecipientUserId == recipientId
            && item.TargetAt == targetUtc).ToListAsync(ct);
        if (existing.Any(item => item.RecipientUserId == recipientId && item.TargetAt == targetUtc
            && item.Status != CourseManagementReminderStatus.Cancelled))
            return;

        db.Reminders.Add(CourseManagementReminder.Create(Guid.NewGuid(), workspaceId, relatedEntityId,
            recipientId, type, targetAt, notifyAt, now));
    }
}
