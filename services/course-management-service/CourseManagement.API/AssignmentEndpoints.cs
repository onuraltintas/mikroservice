using System.Data;
using System.Security.Claims;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CourseManagement.API;

public static class AssignmentEndpoints
{
    public static void MapAssignmentEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/workspaces/{workspaceId:guid}/assignments", async (Guid workspaceId,
            CreateCourseAssignmentRequest request, ClaimsPrincipal user, CourseManagementDbContext db,
            IPublishEndpoint publishEndpoint, CancellationToken ct) =>
        {
            if (request.Id == Guid.Empty || request.CourseId == Guid.Empty || request.DueAt is null
                || string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 160
                || request.Instructions?.Length > 4000 || request.GroupId == Guid.Empty || request.StudentId == Guid.Empty
                || (request.GroupId is null) == (request.StudentId is null))
                return Results.BadRequest(new { code = "Assignment.InvalidInput" });

            var userId = UserId(user);
            var owner = await IsOwner(db, workspaceId, userId, ct);
            if (!owner && !await IsActiveTeacher(db, workspaceId, userId, ct)) return Results.NotFound();

            var courseExists = await db.Courses.AnyAsync(course => course.Id == request.CourseId
                && course.WorkspaceId == workspaceId, ct);
            if (!courseExists) return Results.NotFound();

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            Guid[] recipientIds;
            if (request.GroupId is Guid groupId)
            {
                var group = await db.Groups.SingleOrDefaultAsync(item => item.Id == groupId
                    && item.WorkspaceId == workspaceId && item.CourseId == request.CourseId, ct);
                if (group is null || (!owner && group.TeacherUserId != userId)) return Results.NotFound();
                recipientIds = await (from enrollment in db.Enrollments.AsNoTracking()
                    join student in db.Students.AsNoTracking() on enrollment.StudentId equals student.Id
                    where enrollment.WorkspaceId == workspaceId && enrollment.GroupId == groupId
                        && enrollment.Status == EnrollmentStatus.Active && student.IsActive
                    select student.Id).Distinct().ToArrayAsync(ct);
            }
            else
            {
                var studentId = request.StudentId!.Value;
                if (!await db.Students.AnyAsync(student => student.Id == studentId && student.WorkspaceId == workspaceId
                        && student.IsActive, ct)) return Results.NotFound();
                var enrollment = await (from item in db.Enrollments.AsNoTracking()
                    join courseGroup in db.Groups.AsNoTracking() on item.GroupId equals courseGroup.Id
                    where item.WorkspaceId == workspaceId && item.StudentId == studentId
                        && item.Status == EnrollmentStatus.Active && courseGroup.CourseId == request.CourseId
                        && courseGroup.WorkspaceId == workspaceId && (owner || courseGroup.TeacherUserId == userId)
                    select item.Id).AnyAsync(ct);
                if (!enrollment) return Results.NotFound();
                recipientIds = [studentId];
            }

            if (recipientIds.Length == 0) return Results.Conflict(new { code = "Assignment.NoActiveRecipients" });
            var existing = await db.Assignments.SingleOrDefaultAsync(item => item.Id == request.Id, ct);
            if (existing is not null)
            {
                if (!Matches(existing, workspaceId, request)) return Results.Conflict(new { code = "Assignment.IdempotencyConflict" });
                var existingRecipientCount = await db.AssignmentRecipients.CountAsync(item => item.AssignmentId == existing.Id, ct);
                var existingSubmissionCount = await db.AssignmentSubmissions.CountAsync(item => item.AssignmentId == existing.Id, ct);
                return Results.Ok(ToView(existing, existingRecipientCount, existingSubmissionCount));
            }

            CourseAssignment assignment;
            var now = DateTimeOffset.UtcNow;
            try
            {
                assignment = CourseAssignment.Create(request.Id, workspaceId, request.CourseId, request.GroupId,
                    request.StudentId, userId, request.Title, request.Instructions, request.DueAt.Value, now);
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { code = "Assignment.InvalidInput" });
            }
            db.Assignments.Add(assignment);
            foreach (var studentId in recipientIds)
                db.AssignmentRecipients.Add(CourseAssignmentRecipient.Create(Guid.NewGuid(), workspaceId, assignment.Id, studentId));
            var recipientUserIds = await db.Students.AsNoTracking().Where(student => Enumerable.Contains(recipientIds, student.Id)
                    && student.UserId != null).Select(student => student.UserId!.Value).Distinct().ToArrayAsync(ct);
            await CourseManagementReminderScheduling.ScheduleAssignmentAsync(db, workspaceId, assignment.Id,
                assignment.DueAt, recipientUserIds, now, ct);
            if (recipientUserIds.Length > 0)
                await publishEndpoint.Publish(new CourseManagementAssignmentPublishedV1(assignment.Id, workspaceId,
                    assignment.Title, assignment.DueAt, recipientUserIds), ct);
            AddAudit(db, workspaceId, userId, assignment.Id, "Assignment.Created");
            try
            {
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                return Results.Conflict(new { code = "Assignment.ConcurrentCreation" });
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
            {
                return Results.Conflict(new { code = "Assignment.ConcurrentCreation" });
            }
            return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/assignments/{assignment.Id}",
                ToView(assignment, recipientIds.Length, 0));
        });

        api.MapGet("/workspaces/{workspaceId:guid}/assignments", async (Guid workspaceId, int? skip, int? take, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            var owner = await IsOwner(db, workspaceId, userId, ct);
            var teacher = await IsActiveTeacher(db, workspaceId, userId, ct);
            if (!owner && !teacher) return Results.NotFound();
            if (skip is < 0 || take is < 1 or > 200) return Results.BadRequest(new { code = "Assignment.InvalidPage" });
            var pageSkip = skip ?? 0;
            var pageSize = take ?? 200;
            var assignments = db.Assignments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId);
            if (!owner)
                assignments = assignments.Where(item => item.TeacherUserId == userId
                    || item.GroupId != null && db.Groups.Any(group => group.Id == item.GroupId
                        && group.WorkspaceId == workspaceId && group.TeacherUserId == userId)
                    || item.StudentId != null && db.Enrollments.Any(enrollment => enrollment.WorkspaceId == workspaceId
                        && enrollment.StudentId == item.StudentId && enrollment.Status == EnrollmentStatus.Active
                        && db.Groups.Any(group => group.Id == enrollment.GroupId && group.CourseId == item.CourseId
                            && group.TeacherUserId == userId)));
            var rows = await (from assignment in assignments
                join course in db.Courses.AsNoTracking() on assignment.CourseId equals course.Id
                orderby assignment.DueAt, assignment.Id
                select new
                {
                    assignment,
                    courseName = course.Name,
                    recipientCount = db.AssignmentRecipients.Count(item => item.AssignmentId == assignment.Id),
                    submissionCount = db.AssignmentSubmissions.Count(item => item.AssignmentId == assignment.Id)
                }).Skip(pageSkip).Take(pageSize).ToListAsync(ct);
            return Results.Ok(rows.Select(row => ToView(row.assignment, row.recipientCount, row.submissionCount, row.courseName)));
        });

        api.MapGet("/workspaces/{workspaceId:guid}/assignments/{assignmentId:guid}/submissions", async (Guid workspaceId,
            Guid assignmentId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var assignment = await db.Assignments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == assignmentId
                && item.WorkspaceId == workspaceId, ct);
            if (assignment is null || !await CanManageAssignment(db, assignment, UserId(user), ct)) return Results.NotFound();
            var recipients = await (from recipient in db.AssignmentRecipients.AsNoTracking()
                join student in db.Students.AsNoTracking() on recipient.StudentId equals student.Id
                where recipient.WorkspaceId == workspaceId && recipient.AssignmentId == assignmentId
                orderby student.DisplayName
                select new { student.Id, student.DisplayName }).ToListAsync(ct);
            var submissions = await db.AssignmentSubmissions.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                && item.AssignmentId == assignmentId).ToDictionaryAsync(item => item.StudentId, ct);
            var now = DateTimeOffset.UtcNow;
            var submissionIds = submissions.Values.Select(item => item.Id).ToArray();
            var attachments = submissionIds.Length == 0 ? [] : await db.AssignmentAttachments.AsNoTracking()
                .Where(item => item.ExpiresAt > now && item.SubmissionId != null
                    && Enumerable.Contains(submissionIds, item.SubmissionId.Value)).ToListAsync(ct);
            return Results.Ok(recipients.Select(student => submissions.TryGetValue(student.Id, out var submission)
                ? new AssignmentSubmissionView(student.Id, student.DisplayName, submission.Id, submission.Text,
                    SubmissionStatus(submission, assignment.DueAt), submission.SubmittedAt > assignment.DueAt,
                    submission.SubmittedAt, submission.Score, submission.Feedback, submission.ReviewedAt,
                    attachments.Where(item => item.SubmissionId == submission.Id).Select(AssignmentAttachmentEndpoints.ToView).ToArray())
                : new AssignmentSubmissionView(student.Id, student.DisplayName, null, null, "Pending", false,
                    null, null, null, null, [])));
        });

        api.MapPut("/workspaces/{workspaceId:guid}/assignments/{assignmentId:guid}/submissions/{studentId:guid}/grade",
            async (Guid workspaceId, Guid assignmentId, Guid studentId, ReviewCourseAssignmentRequest request,
                ClaimsPrincipal user, CourseManagementDbContext db, IPublishEndpoint publishEndpoint, CancellationToken ct) =>
        {
            if (request.Score is < 0 or > 100 || request.Score is { } score && decimal.Round(score, 2) != score
                || request.Feedback?.Length > 3000 || request.Score is null && string.IsNullOrWhiteSpace(request.Feedback))
                return Results.BadRequest(new { code = "AssignmentReview.InvalidInput" });
            var assignment = await db.Assignments.SingleOrDefaultAsync(item => item.Id == assignmentId
                && item.WorkspaceId == workspaceId, ct);
            if (assignment is null || !await CanManageAssignment(db, assignment, UserId(user), ct)) return Results.NotFound();
            if (!await db.AssignmentRecipients.AnyAsync(item => item.WorkspaceId == workspaceId
                    && item.AssignmentId == assignmentId && item.StudentId == studentId, ct)) return Results.NotFound();
            var submission = await db.AssignmentSubmissions.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId
                && item.AssignmentId == assignmentId && item.StudentId == studentId, ct);
            if (submission is null) return Results.NotFound();
            var userId = UserId(user);
            submission.Review(request.Score, request.Feedback, userId, DateTimeOffset.UtcNow);
            var studentUserId = await db.Students.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                    && item.Id == studentId && item.IsActive).Select(item => item.UserId).SingleOrDefaultAsync(ct);
            if (studentUserId.HasValue)
                await publishEndpoint.Publish(new CourseManagementAssignmentReviewedV1(assignmentId, workspaceId,
                    studentUserId.Value), ct);
            AddAudit(db, workspaceId, userId, submission.Id, "Assignment.Reviewed");
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToView(submission, assignment.DueAt));
        });

        api.MapGet("/me/assignments", async (ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            var recipients = await (from recipient in db.AssignmentRecipients.AsNoTracking()
                join student in db.Students.AsNoTracking() on recipient.StudentId equals student.Id
                join membership in db.Memberships.AsNoTracking() on new { student.WorkspaceId, UserId = student.UserId!.Value }
                    equals new { membership.WorkspaceId, membership.UserId }
                join assignment in db.Assignments.AsNoTracking() on recipient.AssignmentId equals assignment.Id
                join workspace in db.Workspaces.AsNoTracking() on recipient.WorkspaceId equals workspace.Id
                join course in db.Courses.AsNoTracking() on assignment.CourseId equals course.Id
                where student.UserId == userId && student.IsActive && membership.IsActive
                    && membership.Role == WorkspaceRole.Student
                orderby assignment.DueAt
                select new { student.Id, assignment, workspaceName = workspace.Name, courseName = course.Name })
                .Take(200).ToListAsync(ct);
            var assignmentIds = recipients.Select(row => row.assignment.Id).Distinct().ToArray();
            var studentIds = recipients.Select(row => row.Id).Distinct().ToArray();
            var now = DateTimeOffset.UtcNow;
            var submissions = assignmentIds.Length == 0 ? new Dictionary<Guid, CourseAssignmentSubmission>()
                : await db.AssignmentSubmissions.AsNoTracking().Where(item => Enumerable.Contains(studentIds, item.StudentId)
                    && Enumerable.Contains(assignmentIds, item.AssignmentId)).ToDictionaryAsync(item => item.AssignmentId, ct);
            var attachments = assignmentIds.Length == 0 ? [] : await db.AssignmentAttachments.AsNoTracking()
                .Where(item => Enumerable.Contains(assignmentIds, item.AssignmentId) && item.ExpiresAt > now).ToListAsync(ct);
            return Results.Ok(recipients.Select(row =>
            {
                submissions.TryGetValue(row.assignment.Id, out var submission);
                return new StudentAssignmentView(row.assignment.Id, row.assignment.WorkspaceId, row.workspaceName,
                    row.assignment.CourseId, row.courseName, row.assignment.Title, row.assignment.Instructions,
                    row.assignment.DueAt, SubmissionStatus(submission, row.assignment.DueAt),
                    submission is not null && submission.SubmittedAt > row.assignment.DueAt,
                    submission?.Text, submission?.SubmittedAt, submission?.Score, submission?.Feedback, submission?.ReviewedAt,
                    attachments.Where(item => item.AssignmentId == row.assignment.Id
                        && item.Purpose == CourseAssignmentAttachmentPurpose.TeacherMaterial)
                        .Select(AssignmentAttachmentEndpoints.ToView).ToArray(),
                    attachments.Where(item => item.AssignmentId == row.assignment.Id
                        && item.Purpose == CourseAssignmentAttachmentPurpose.StudentSubmission && item.StudentId == row.Id)
                        .Select(AssignmentAttachmentEndpoints.ToView).ToArray());
            }));
        });

        api.MapPost("/me/assignments/{assignmentId:guid}/submission", async (Guid assignmentId,
            SubmitCourseAssignmentRequest request, ClaimsPrincipal user, CourseManagementDbContext db,
            IPublishEndpoint publishEndpoint, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Trim().Length > 8000)
                return Results.BadRequest(new { code = "AssignmentSubmission.InvalidText" });
            var userId = UserId(user);
            var recipient = await (from item in db.AssignmentRecipients.AsNoTracking()
                join student in db.Students.AsNoTracking() on item.StudentId equals student.Id
                join membership in db.Memberships.AsNoTracking() on item.WorkspaceId equals membership.WorkspaceId
                where item.AssignmentId == assignmentId && student.UserId == userId && student.IsActive
                    && membership.UserId == userId && membership.IsActive && membership.Role == WorkspaceRole.Student
                select new { item.WorkspaceId, item.StudentId }).SingleOrDefaultAsync(ct);
            if (recipient is null) return Results.NotFound();
            var assignment = await db.Assignments.SingleOrDefaultAsync(item => item.Id == assignmentId
                && item.WorkspaceId == recipient.WorkspaceId, ct);
            if (assignment is null) return Results.NotFound();
            var now = DateTimeOffset.UtcNow;
            var submission = await db.AssignmentSubmissions.SingleOrDefaultAsync(item => item.WorkspaceId == recipient.WorkspaceId
                && item.AssignmentId == assignmentId && item.StudentId == recipient.StudentId, ct);
            var created = submission is null;
            if (created)
            {
                submission = CourseAssignmentSubmission.Create(Guid.NewGuid(), recipient.WorkspaceId, assignmentId,
                    recipient.StudentId, request.Text, now);
                db.AssignmentSubmissions.Add(submission);
            }
            else if (!string.Equals(submission!.Text, request.Text.Trim(), StringComparison.Ordinal))
            {
                submission.UpdateSubmission(request.Text, now);
            }
            else return Results.Ok(ToView(submission!, assignment.DueAt));
            AddAudit(db, recipient.WorkspaceId, userId, submission!.Id,
                created ? "Assignment.Submitted" : "Assignment.Revised");
            await publishEndpoint.Publish(new CourseManagementAssignmentSubmittedV1(assignmentId,
                recipient.WorkspaceId, assignment.TeacherUserId), ct);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                return Results.Conflict(new { code = "AssignmentSubmission.ConcurrentUpdate" });
            }
            return created
                ? Results.Created($"/api/course-management/v1/me/assignments/{assignmentId}/submission", ToView(submission!, assignment.DueAt))
                : Results.Ok(ToView(submission!, assignment.DueAt));
        });

        api.MapAssignmentAttachmentEndpoints();
    }

    private static bool Matches(CourseAssignment assignment, Guid workspaceId, CreateCourseAssignmentRequest request) =>
        assignment.WorkspaceId == workspaceId && assignment.CourseId == request.CourseId
        && assignment.GroupId == request.GroupId && assignment.StudentId == request.StudentId
        && assignment.Title == request.Title.Trim() && assignment.Instructions == Normalize(request.Instructions)
        && assignment.DueAt == request.DueAt!.Value.ToUniversalTime();

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    internal static async Task<bool> CanManageAssignment(CourseManagementDbContext db, CourseAssignment assignment,
        Guid userId, CancellationToken ct)
    {
        if (await IsOwner(db, assignment.WorkspaceId, userId, ct)) return true;
        if (!await IsActiveTeacher(db, assignment.WorkspaceId, userId, ct)) return false;
        if (assignment.TeacherUserId == userId) return true;
        if (assignment.GroupId is Guid groupId)
            return await db.Groups.AnyAsync(group => group.Id == groupId && group.WorkspaceId == assignment.WorkspaceId
                && group.TeacherUserId == userId, ct);
        return assignment.StudentId is Guid studentId && await (from enrollment in db.Enrollments
            join courseGroup in db.Groups on enrollment.GroupId equals courseGroup.Id
            where enrollment.WorkspaceId == assignment.WorkspaceId && enrollment.StudentId == studentId
                && enrollment.Status == EnrollmentStatus.Active && courseGroup.CourseId == assignment.CourseId
                && courseGroup.TeacherUserId == userId
            select enrollment.Id).AnyAsync(ct);
    }

    private static Task<bool> IsOwner(CourseManagementDbContext db, Guid workspaceId, Guid userId, CancellationToken ct) =>
        db.Workspaces.AnyAsync(workspace => workspace.Id == workspaceId && workspace.OwnerUserId == userId, ct);

    private static Task<bool> IsActiveTeacher(CourseManagementDbContext db, Guid workspaceId, Guid userId, CancellationToken ct) =>
        db.Memberships.AnyAsync(member => member.WorkspaceId == workspaceId && member.UserId == userId
            && member.IsActive && member.Role == WorkspaceRole.Teacher, ct);

    internal static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);

    internal static void AddAudit(CourseManagementDbContext db, Guid workspaceId, Guid actorId, Guid targetId, string action) =>
        db.AuditEntries.Add(WorkspaceAuditEntry.Create(workspaceId, actorId, targetId, action));

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string SubmissionStatus(CourseAssignmentSubmission? submission, DateTimeOffset dueAt) => submission is null
        ? "Pending" : submission.ReviewedAt is not null ? "Reviewed" : submission.SubmittedAt > dueAt ? "Late" : "Submitted";

    private static AssignmentView ToView(CourseAssignment assignment, int recipientCount, int? submissionCount,
        string? courseName = null) => new(assignment.Id, assignment.WorkspaceId, assignment.CourseId, courseName,
        assignment.GroupId, assignment.StudentId, assignment.TeacherUserId, assignment.Title, assignment.Instructions,
        assignment.DueAt, assignment.CreatedAt, recipientCount, submissionCount);

    private static AssignmentSubmissionView ToView(CourseAssignmentSubmission submission, DateTimeOffset dueAt) =>
        new(submission.StudentId, string.Empty, submission.Id, submission.Text, SubmissionStatus(submission, dueAt),
            submission.SubmittedAt > dueAt, submission.SubmittedAt, submission.Score, submission.Feedback, submission.ReviewedAt, []);
}

public sealed record CreateCourseAssignmentRequest(Guid Id, Guid CourseId, Guid? GroupId, Guid? StudentId,
    string Title, string? Instructions, DateTimeOffset? DueAt);
public sealed record SubmitCourseAssignmentRequest(string? Text);
public sealed record ReviewCourseAssignmentRequest(decimal? Score, string? Feedback);
public sealed record AssignmentView(Guid Id, Guid WorkspaceId, Guid CourseId, string? CourseName, Guid? GroupId,
    Guid? StudentId, Guid TeacherUserId, string Title, string? Instructions, DateTimeOffset DueAt,
    DateTimeOffset CreatedAt, int RecipientCount, int? SubmissionCount);
public sealed record StudentAssignmentView(Guid Id, Guid WorkspaceId, string WorkspaceName, Guid CourseId,
    string CourseName, string Title, string? Instructions, DateTimeOffset DueAt, string Status, bool IsLate,
    string? SubmissionText, DateTimeOffset? SubmittedAt, decimal? Score, string? Feedback, DateTimeOffset? ReviewedAt,
    CourseAssignmentAttachmentView[] Materials, CourseAssignmentAttachmentView[] SubmissionAttachments);
public sealed record AssignmentSubmissionView(Guid StudentId, string StudentName, Guid? SubmissionId, string? Text,
    string Status, bool IsLate, DateTimeOffset? SubmittedAt, decimal? Score, string? Feedback, DateTimeOffset? ReviewedAt,
    CourseAssignmentAttachmentView[] Attachments);
