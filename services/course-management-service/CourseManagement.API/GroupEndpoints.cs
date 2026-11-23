using System.Security.Claims;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

public sealed record GroupStudentsRequest(Guid[]? StudentIds);
public sealed record MoveGroupStudentRequest(Guid ToGroupId);

/// <summary>
/// A group's end and its students (user decision 23.11.2026):
/// - Archive: the group takes no new students, lessons or homework. Its planned lessons are cancelled and its students
///   leave it; attendance, homework and payments stay. The owner confirms first when there is something to cancel.
/// - Delete: only a group with no history (no lesson has started, no homework, no attendance, no make-up credit). A group
///   with history is archived instead (Group.InUse).
/// - Many students join a group in one step (CSV import, the student list), and a student moves from one group to another
///   in one step: they leave the old group's planned lessons and join the new group's.
/// </summary>
public static class GroupEndpoints
{
    public const int MaxStudentsPerRequest = 500;

    public static void MapGroupEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/workspaces/{workspaceId:guid}/groups/{groupId:guid}/archive", async (Guid workspaceId, Guid groupId, bool? force,
            ClaimsPrincipal user, CourseManagementDbContext db, IPublishEndpoint publishEndpoint, CancellationToken ct) =>
        {
            if (await SetupEndpoints.OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var group = await db.Groups.SingleOrDefaultAsync(item => item.Id == groupId && item.WorkspaceId == workspaceId, ct);
            if (group is null) return Results.NotFound();
            if (group.ArchivedAt is not null) return Results.NoContent();
            var now = DateTimeOffset.UtcNow;
            var lessons = await PlannedLessons(db, workspaceId, groupId, now).ToListAsync(ct);
            var enrollments = await db.Enrollments.Where(item => item.WorkspaceId == workspaceId && item.GroupId == groupId
                && item.Status == EnrollmentStatus.Active).ToListAsync(ct);
            if ((lessons.Count > 0 || enrollments.Count > 0) && force != true)
                return Results.Conflict(new { code = "Group.HasPlannedLessonsOrStudents", lessons = lessons.Count, students = enrollments.Count });
            var userId = SetupEndpoints.UserId(user);
            var lessonIds = lessons.Select(lesson => lesson.Id).ToList();
            var participantStudentIds = await db.LessonParticipants.AsNoTracking()
                .Where(participant => participant.WorkspaceId == workspaceId && participant.IsActive && lessonIds.Contains(participant.LessonSessionId))
                .Select(participant => participant.StudentId).Distinct().ToListAsync(ct);
            var studentUserIds = await LearningEndpoints.GetLinkedStudentUserIds(db, workspaceId, participantStudentIds, ct);
            foreach (var lesson in lessons)
            {
                lesson.Cancel();
                await CourseManagementReminderScheduling.CancelEntityAsync(db, workspaceId, lesson.Id, ct);
                // The group has ended: a make-up lesson planned into it goes back to its student; no new credits are given.
                var makeup = await db.MakeupCredits.SingleOrDefaultAsync(credit => credit.WorkspaceId == workspaceId && credit.MakeupLessonId == lesson.Id, ct);
                if (makeup is { Status: MakeupCreditStatus.Scheduled }) makeup.ReleaseFromCancelledLesson(now);
                if (studentUserIds.Length > 0)
                    await publishEndpoint.Publish(new CourseManagementLessonCancelledV1(lesson.Id, workspaceId, studentUserIds, lesson.StartsAt), ct);
            }
            foreach (var enrollment in enrollments)
            {
                enrollment.Archive();
                await CancelFutureParticipation(db, workspaceId, groupId, enrollment.StudentId, now, ct);
            }
            group.Archive(now);
            Audit(db, workspaceId, userId, groupId, "Group.Archived");
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { cancelledLessons = lessons.Count, removedStudents = enrollments.Count });
        });

        api.MapPost("/workspaces/{workspaceId:guid}/groups/{groupId:guid}/restore", async (Guid workspaceId, Guid groupId,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await SetupEndpoints.OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var group = await db.Groups.SingleOrDefaultAsync(item => item.Id == groupId && item.WorkspaceId == workspaceId, ct);
            if (group is null) return Results.NotFound();
            if (group.ArchivedAt is null) return Results.NoContent();
            group.Restore();
            Audit(db, workspaceId, SetupEndpoints.UserId(user), groupId, "Group.Restored");
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        api.MapDelete("/workspaces/{workspaceId:guid}/groups/{groupId:guid}", async (Guid workspaceId, Guid groupId, bool? force,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await SetupEndpoints.OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var group = await db.Groups.SingleOrDefaultAsync(item => item.Id == groupId && item.WorkspaceId == workspaceId, ct);
            if (group is null) return Results.NotFound();
            var now = DateTimeOffset.UtcNow;
            var lessons = await db.LessonSessions.Where(lesson => lesson.WorkspaceId == workspaceId && lesson.GroupId == groupId).ToListAsync(ct);
            var lessonIds = lessons.Select(lesson => lesson.Id).ToList();
            // History stays: a group whose lessons have started, or that has homework, attendance, a make-up credit or a
            // charge from one of its lessons, is archived instead.
            var inUse = lessons.Any(lesson => lesson.StartsAt <= now || lesson.Status == LessonSessionStatus.Completed)
                || await db.Assignments.AnyAsync(item => item.WorkspaceId == workspaceId && item.GroupId == groupId, ct)
                || await db.AttendanceRecords.AnyAsync(item => item.WorkspaceId == workspaceId && lessonIds.Contains(item.LessonSessionId), ct)
                || await db.MakeupCredits.AnyAsync(item => item.WorkspaceId == workspaceId && (lessonIds.Contains(item.SourceLessonId)
                    || item.MakeupLessonId != null && lessonIds.Contains(item.MakeupLessonId.Value) && item.Status != MakeupCreditStatus.Scheduled), ct)
                || await db.PackageMovements.AnyAsync(item => item.LessonSessionId != null && lessonIds.Contains(item.LessonSessionId.Value), ct)
                || await db.Installments.AnyAsync(item => item.SourceLessonId != null && lessonIds.Contains(item.SourceLessonId.Value), ct);
            if (inUse) return Results.Conflict(new { code = "Group.InUse" });
            var planned = lessons.Count(lesson => lesson.Status == LessonSessionStatus.Scheduled);
            var enrollments = await db.Enrollments.Where(item => item.WorkspaceId == workspaceId && item.GroupId == groupId).ToListAsync(ct);
            var students = enrollments.Count(item => item.Status == EnrollmentStatus.Active);
            if ((planned > 0 || students > 0) && force != true)
                return Results.Conflict(new { code = "Group.HasPlannedLessonsOrStudents", lessons = planned, students });
            foreach (var credit in await db.MakeupCredits.Where(item => item.WorkspaceId == workspaceId && item.MakeupLessonId != null
                && lessonIds.Contains(item.MakeupLessonId.Value) && item.Status == MakeupCreditStatus.Scheduled).ToListAsync(ct))
                credit.ReleaseFromCancelledLesson(now);
            foreach (var lessonId in lessonIds) await CourseManagementReminderScheduling.CancelEntityAsync(db, workspaceId, lessonId, ct);
            db.LessonParticipants.RemoveRange(await db.LessonParticipants.Where(item => lessonIds.Contains(item.LessonSessionId)).ToListAsync(ct));
            // Lessons, then their series, then the enrollments and the group: each save keeps the foreign keys whole.
            await db.SaveChangesAsync(ct);
            db.LessonSessions.RemoveRange(lessons);
            await db.SaveChangesAsync(ct);
            db.LessonSeries.RemoveRange(await db.LessonSeries.Where(item => item.WorkspaceId == workspaceId && item.GroupId == groupId).ToListAsync(ct));
            db.Enrollments.RemoveRange(enrollments);
            await db.SaveChangesAsync(ct);
            db.Groups.Remove(group);
            Audit(db, workspaceId, SetupEndpoints.UserId(user), groupId, "Group.Deleted");
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { deletedLessons = planned, removedStudents = students });
        });

        // Many students into one group (the student list's selection). Students already in the group are counted, not
        // added twice; a student who is not in the workspace stops the whole request.
        api.MapPost("/workspaces/{workspaceId:guid}/groups/{groupId:guid}/students", async (Guid workspaceId, Guid groupId,
            GroupStudentsRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await SetupEndpoints.OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var ids = (request.StudentIds ?? []).Where(id => id != Guid.Empty).Distinct().ToArray();
            if (ids.Length is 0 or > MaxStudentsPerRequest) return Results.BadRequest(new { code = "Group.InvalidStudents" });
            var group = await db.Groups.SingleOrDefaultAsync(item => item.Id == groupId && item.WorkspaceId == workspaceId, ct);
            if (group is null) return Results.NotFound();
            if (group.ArchivedAt is not null) return Results.Conflict(new { code = "Group.Archived" });
            var known = await db.Students.CountAsync(student => student.WorkspaceId == workspaceId && student.IsActive && ids.Contains(student.Id), ct);
            if (known != ids.Length) return Results.NotFound();
            var userId = SetupEndpoints.UserId(user);
            var added = 0;
            foreach (var studentId in ids)
                if (await JoinAsync(db, workspaceId, groupId, studentId, userId, DateTimeOffset.UtcNow, ct)) added++;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { added, alreadyInGroup = ids.Length - added,
                fullRoomLessons = await LearningEndpoints.FullRoomLessons(db, workspaceId, groupId, ct) });
        });

        // One step instead of "remove, then add": the student leaves the old group (and its planned lessons) and joins the
        // new one (and its planned lessons). Attendance and homework of the old group stay.
        api.MapPost("/workspaces/{workspaceId:guid}/groups/{groupId:guid}/students/{studentId:guid}/move", async (Guid workspaceId,
            Guid groupId, Guid studentId, MoveGroupStudentRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await SetupEndpoints.OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            if (request.ToGroupId == Guid.Empty || request.ToGroupId == groupId) return Results.BadRequest(new { code = "Group.InvalidMove" });
            var enrollment = await db.Enrollments.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.GroupId == groupId
                && item.StudentId == studentId && item.Status == EnrollmentStatus.Active, ct);
            if (enrollment is null) return Results.NotFound();
            var target = await db.Groups.SingleOrDefaultAsync(item => item.Id == request.ToGroupId && item.WorkspaceId == workspaceId, ct);
            if (target is null) return Results.NotFound();
            if (target.ArchivedAt is not null) return Results.Conflict(new { code = "Group.Archived" });
            var userId = SetupEndpoints.UserId(user);
            var now = DateTimeOffset.UtcNow;
            await LeaveAsync(db, workspaceId, groupId, enrollment, userId, now, ct);
            await JoinAsync(db, workspaceId, target.Id, studentId, userId, now, ct);
            Audit(db, workspaceId, userId, studentId, "Enrollment.Moved");
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { groupId = target.Id, fullRoomLessons = await LearningEndpoints.FullRoomLessons(db, workspaceId, target.Id, ct) });
        });
    }

    /// <summary>
    /// The student joins the group, and the group's planned lessons from now on list them (before 23.11.2026 only lessons
    /// planned after joining did). Returns false when the student was already in the group. Nothing is saved here.
    /// </summary>
    internal static async Task<bool> JoinAsync(CourseManagementDbContext db, Guid workspaceId, Guid groupId, Guid studentId, Guid actorId,
        DateTimeOffset now, CancellationToken ct)
    {
        var existing = await db.Enrollments.SingleOrDefaultAsync(item => item.GroupId == groupId && item.StudentId == studentId, ct);
        if (existing is { Status: EnrollmentStatus.Active }) return false;
        if (existing is null)
        {
            existing = StudentGroupEnrollment.Create(Guid.NewGuid(), workspaceId, groupId, studentId, now);
            db.Enrollments.Add(existing);
            Audit(db, workspaceId, actorId, existing.Id, "Enrollment.Created");
        }
        else
        {
            existing.Reactivate();
            Audit(db, workspaceId, actorId, existing.Id, "Enrollment.Restored");
        }
        var lessonIds = await PlannedLessons(db, workspaceId, groupId, now).Select(lesson => lesson.Id).ToListAsync(ct);
        var listed = await db.LessonParticipants.Where(item => item.StudentId == studentId && lessonIds.Contains(item.LessonSessionId))
            .Select(item => item.LessonSessionId).ToListAsync(ct);
        // A participation the student cancelled stays cancelled; only lessons without the student get them.
        db.LessonParticipants.AddRange(lessonIds.Except(listed).Select(lessonId => LessonParticipant.Create(Guid.NewGuid(), workspaceId, lessonId, studentId)));
        return true;
    }

    /// <summary>The student leaves the group: the enrollment is archived and the group's planned lessons drop them. Nothing is saved here.</summary>
    internal static async Task LeaveAsync(CourseManagementDbContext db, Guid workspaceId, Guid groupId, StudentGroupEnrollment enrollment,
        Guid actorId, DateTimeOffset now, CancellationToken ct)
    {
        enrollment.Archive();
        await CancelFutureParticipation(db, workspaceId, groupId, enrollment.StudentId, now, ct);
        Audit(db, workspaceId, actorId, enrollment.Id, "Enrollment.Archived");
    }

    private static async Task CancelFutureParticipation(CourseManagementDbContext db, Guid workspaceId, Guid groupId, Guid studentId,
        DateTimeOffset now, CancellationToken ct)
    {
        var participants = await (from participant in db.LessonParticipants
            join lesson in db.LessonSessions on participant.LessonSessionId equals lesson.Id
            where participant.WorkspaceId == workspaceId && participant.StudentId == studentId && participant.IsActive
                && lesson.GroupId == groupId && lesson.Status == LessonSessionStatus.Scheduled && lesson.StartsAt > now
            select participant).ToListAsync(ct);
        foreach (var participant in participants) participant.Cancel(now);
    }

    private static IQueryable<LessonSession> PlannedLessons(CourseManagementDbContext db, Guid workspaceId, Guid groupId, DateTimeOffset now) =>
        db.LessonSessions.Where(lesson => lesson.WorkspaceId == workspaceId && lesson.GroupId == groupId
            && lesson.Status == LessonSessionStatus.Scheduled && lesson.StartsAt > now);

    private static void Audit(CourseManagementDbContext db, Guid workspaceId, Guid actorId, Guid entityId, string action) =>
        db.AuditEntries.Add(WorkspaceAuditEntry.Create(workspaceId, actorId, entityId, action));
}
