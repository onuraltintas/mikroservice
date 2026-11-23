using System.Security.Claims;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

public sealed record SetupProgressRequest(string[]? Skipped, bool Closed);
public sealed record WorkspaceProfileRequest(string? Name, InstitutionCategory? InstitutionCategory);
public sealed record UpdateGroupRequest(string? Name, Guid? TeacherUserId, bool MoveFutureLessons, bool Force);
public sealed record OneToOneTeacherRequest(Guid? TeacherUserId);

/// <summary>
/// The setup guide (20.11.2026): what is done is read from the data, so work done by hand counts too; only skipped
/// steps and "closed" are remembered. Also the edits the guide needs and the panel lacked: the institution's name and
/// kind, a group's name and teacher, taking a student out of a group, and a student's one-to-one teacher.
/// All of it is the owner's.
/// </summary>
public static class SetupEndpoints
{
    public static void MapSetupEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/workspaces/{workspaceId:guid}/setup", async (Guid workspaceId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var workspace = await OwnedAsync(db, workspaceId, user, ct);
            if (workspace is null) return Results.NotFound();
            var now = DateTimeOffset.UtcNow;
            var teachers = await db.Memberships.CountAsync(member => member.WorkspaceId == workspaceId && member.IsActive && member.Role == WorkspaceRole.Teacher, ct);
            var invited = await db.TeacherEmailInvitations.CountAsync(item => item.WorkspaceId == workspaceId
                && item.Status == InvitationStatus.Pending && item.ExpiresAt > now, ct);
            var rooms = await db.Rooms.CountAsync(room => room.WorkspaceId == workspaceId, ct);
            var courses = await db.Courses.CountAsync(course => course.WorkspaceId == workspaceId, ct);
            var groups = await db.Groups.CountAsync(group => group.WorkspaceId == workspaceId && group.ArchivedAt == null, ct);
            var activeStudents = db.Students.Where(student => student.WorkspaceId == workspaceId && student.IsActive);
            var students = await activeStudents.CountAsync(ct);
            var unplaced = await activeStudents.CountAsync(student => student.OneToOneTeacherUserId == null
                && !db.Enrollments.Any(enrollment => enrollment.StudentId == student.Id && enrollment.Status == EnrollmentStatus.Active), ct);
            var withoutFee = await activeStudents.CountAsync(student => !db.FeeAgreements.Any(agreement => agreement.StudentId == student.Id
                && agreement.WorkspaceId == workspaceId && agreement.IsActive), ct);
            var futureLessons = await db.LessonSessions.CountAsync(lesson => lesson.WorkspaceId == workspaceId
                && lesson.Status == LessonSessionStatus.Scheduled && lesson.StartsAt > now, ct);
            var consent = await db.EmailSettings.AsNoTracking().Where(item => item.WorkspaceId == workspaceId)
                .Select(item => item.InformedConsentAt).FirstOrDefaultAsync(ct);
            var skipped = (workspace.SetupSkippedSteps ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);
            var done = new Dictionary<string, bool>
            {
                ["profile"] = workspace.InstitutionCategory is not null,
                ["teachers"] = teachers + invited > 0,
                ["rooms"] = rooms > 0,
                ["courses"] = courses > 0 && groups > 0,
                ["students"] = students > 0 && unplaced == 0,
                ["fees"] = students > 0 && withoutFee == 0,
                ["schedule"] = futureLessons > 0,
                ["emails"] = consent is not null
            };
            return Results.Ok(new
            {
                workspace.Name,
                institutionCategory = workspace.InstitutionCategory?.ToString(),
                closed = workspace.SetupClosedAt is not null,
                steps = Workspace.SetupSteps.Select(step => new
                {
                    id = step,
                    status = done[step] ? "done" : skipped.Contains(step) ? "skipped" : "open"
                }),
                counts = new { teachers, invited, rooms, courses, groups, students, unplaced, withoutFee, futureLessons }
            });
        });

        api.MapPut("/workspaces/{workspaceId:guid}/setup", async (Guid workspaceId, SetupProgressRequest request, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            var workspace = await OwnedAsync(db, workspaceId, user, ct);
            if (workspace is null) return Results.NotFound();
            workspace.SetSetupProgress(request.Skipped ?? [], request.Closed, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        api.MapPut("/workspaces/{workspaceId:guid}/profile", async (Guid workspaceId, WorkspaceProfileRequest request, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            var workspace = await OwnedAsync(db, workspaceId, user, ct);
            if (workspace is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200
                || request.InstitutionCategory is { } category && !Enum.IsDefined(category))
                return Results.BadRequest(new { code = "Workspace.InvalidProfile" });
            workspace.UpdateProfile(request.Name, request.InstitutionCategory);
            db.AuditEntries.Add(WorkspaceAuditEntry.Create(workspaceId, UserId(user), workspaceId, "Workspace.ProfileUpdated"));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { workspace.Name, institutionCategory = workspace.InstitutionCategory?.ToString() });
        });

        // A group's name and teacher. Moving the future lessons too is the owner's choice; a lesson that would clash with
        // the new teacher's other lessons stops the change unless the owner confirms (Force).
        api.MapPut("/workspaces/{workspaceId:guid}/groups/{groupId:guid}", async (Guid workspaceId, Guid groupId, UpdateGroupRequest request,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var group = await db.Groups.SingleOrDefaultAsync(item => item.Id == groupId && item.WorkspaceId == workspaceId, ct);
            if (group is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160)
                return Results.BadRequest(new { code = "Group.InvalidInput" });
            var userId = UserId(user);
            var now = DateTimeOffset.UtcNow;
            var moved = 0;
            if (request.TeacherUserId is { } teacher && teacher != group.TeacherUserId)
            {
                if (teacher != userId && !await LearningEndpoints.IsActiveTeacher(db, workspaceId, teacher, ct))
                    return Results.BadRequest(new { code = "Group.InvalidTeacher" });
                if (request.MoveFutureLessons)
                {
                    var future = await db.LessonSessions.Where(lesson => lesson.WorkspaceId == workspaceId && lesson.GroupId == groupId
                        && lesson.Status == LessonSessionStatus.Scheduled && lesson.StartsAt > now).ToListAsync(ct);
                    // Live test 21.11.2026: the new teacher's time off and working hours count too, not only their other lessons.
                    var timeOff = await db.TeacherTimeOffs.AsNoTracking()
                        .Where(item => item.WorkspaceId == workspaceId && item.TeacherUserId == teacher && item.EndsAt > now)
                        .Select(item => new { item.StartsAt, item.EndsAt }).ToListAsync(ct);
                    var windows = await db.TeacherAvailabilityWindows.AsNoTracking()
                        .Where(item => item.WorkspaceId == workspaceId && item.TeacherUserId == teacher).ToListAsync(ct);
                    int clashes = 0, overlapping = 0, duringTimeOff = 0, outsideHours = 0;
                    foreach (var lesson in future)
                    {
                        var other = await db.LessonSessions.AnyAsync(item => item.WorkspaceId == workspaceId && item.TeacherUserId == teacher
                            && item.Status == LessonSessionStatus.Scheduled && item.Id != lesson.Id
                            && item.StartsAt < lesson.EndsAt && lesson.StartsAt < item.EndsAt, ct);
                        var off = timeOff.Any(item => item.StartsAt < lesson.EndsAt && lesson.StartsAt < item.EndsAt);
                        var outside = !LearningEndpoints.FitsTeacherAvailability((lesson.StartsAt, lesson.EndsAt), windows);
                        if (other) overlapping++;
                        if (off) duringTimeOff++;
                        if (outside) outsideHours++;
                        if (other || off || outside) clashes++;
                    }
                    if (clashes > 0 && !request.Force)
                        return Results.Conflict(new { code = "Group.TeacherConflict", clashes, overlapping, duringTimeOff, outsideHours });
                    foreach (var lesson in future) lesson.ChangeTeacher(teacher);
                    foreach (var series in await db.LessonSeries.Where(item => item.WorkspaceId == workspaceId && item.GroupId == groupId).ToListAsync(ct))
                        series.ChangeTeacher(teacher);
                    moved = future.Count;
                }
                group.ChangeTeacher(teacher);
                db.AuditEntries.Add(WorkspaceAuditEntry.Create(workspaceId, userId, groupId, "Group.TeacherChanged"));
            }
            if (group.Name != request.Name.Trim())
            {
                group.Rename(request.Name);
                db.AuditEntries.Add(WorkspaceAuditEntry.Create(workspaceId, userId, groupId, "Group.Renamed"));
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { group.Id, group.Name, group.TeacherUserId, movedLessons = moved });
        });

        // A student leaves a group: the enrollment is archived (attendance and homework stay); the group's lessons from now
        // on no longer list the student.
        api.MapDelete("/workspaces/{workspaceId:guid}/groups/{groupId:guid}/students/{studentId:guid}", async (Guid workspaceId, Guid groupId,
            Guid studentId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var enrollment = await db.Enrollments.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.GroupId == groupId
                && item.StudentId == studentId, ct);
            if (enrollment is null) return Results.NotFound();
            if (enrollment.Status == EnrollmentStatus.Archived) return Results.NoContent();
            await GroupEndpoints.LeaveAsync(db, workspaceId, groupId, enrollment, UserId(user), DateTimeOffset.UtcNow, ct);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // The teacher of a student's one-to-one lessons: sees the student like a group's teacher does. May be the owner,
        // a teacher, or a teacher invited by e-mail who has not accepted yet.
        api.MapPut("/workspaces/{workspaceId:guid}/students/{studentId:guid}/one-to-one-teacher", async (Guid workspaceId, Guid studentId,
            OneToOneTeacherRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var student = await db.Students.SingleOrDefaultAsync(item => item.Id == studentId && item.WorkspaceId == workspaceId, ct);
            if (student is null) return Results.NotFound();
            var userId = UserId(user);
            if (request.TeacherUserId is { } teacher && teacher != userId && !await LearningEndpoints.IsActiveTeacher(db, workspaceId, teacher, ct))
                return Results.BadRequest(new { code = "Student.InvalidTeacher" });
            student.SetOneToOneTeacher(request.TeacherUserId);
            db.AuditEntries.Add(WorkspaceAuditEntry.Create(workspaceId, userId, studentId, "Student.OneToOneTeacherChanged"));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { student.Id, student.OneToOneTeacherUserId });
        });
    }

    internal static Task<Workspace?> OwnedAsync(CourseManagementDbContext db, Guid workspaceId, ClaimsPrincipal user, CancellationToken ct) =>
        db.Workspaces.SingleOrDefaultAsync(workspace => workspace.Id == workspaceId && workspace.OwnerUserId == UserId(user), ct);

    internal static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
}
