using CourseManagement.Application.CourseAssignments;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

public static class AssignmentAttachmentEndpoints
{
    public static void MapAssignmentAttachmentEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/workspaces/{workspaceId:guid}/assignments/{assignmentId:guid}/attachments", async (
            Guid workspaceId, Guid assignmentId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var assignment = await db.Assignments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == assignmentId
                && item.WorkspaceId == workspaceId, ct);
            if (assignment is null || !await AssignmentEndpoints.CanManageAssignment(db, assignment,
                    AssignmentEndpoints.UserId(user), ct)) return Results.NotFound();
            var now = DateTimeOffset.UtcNow;
            var attachments = await db.AssignmentAttachments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                && item.AssignmentId == assignmentId && item.ExpiresAt > now).OrderBy(item => item.UploadedAt).ToListAsync(ct);
            return Results.Ok(attachments.Select(ToView));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/assignments/{assignmentId:guid}/attachments", async (
            Guid workspaceId, Guid assignmentId, string fileName, HttpRequest request, ClaimsPrincipal user,
            CourseManagementDbContext db, ICourseAssignmentAttachmentStorage storage,
            ICourseAssignmentAttachmentScanner scanner, CourseAssignmentAttachmentCleanupQueue cleanupQueue,
            CancellationToken ct) =>
        {
            var assignment = await db.Assignments.SingleOrDefaultAsync(item => item.Id == assignmentId
                && item.WorkspaceId == workspaceId, ct);
            var userId = AssignmentEndpoints.UserId(user);
            if (assignment is null || !await AssignmentEndpoints.CanManageAssignment(db, assignment, userId, ct))
                return Results.NotFound();
            if (DateTimeOffset.UtcNow >= assignment.DueAt.AddDays(180))
                return Results.Conflict(new { code = "AssignmentAttachment.RetentionExpired" });
            var contentType = CourseAssignmentAttachmentPolicy.ContentTypeForFileName(fileName);
            var requestContentType = request.ContentType?.Split(';', 2)[0].Trim().ToLowerInvariant();
            if (contentType is null || !string.Equals(contentType, requestContentType, StringComparison.Ordinal))
                return Results.BadRequest(new { code = "AssignmentAttachment.UnsupportedFile" });
            if (request.ContentLength > CourseAssignmentAttachmentPolicy.MaxFileSizeBytes)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            var attachmentId = Guid.NewGuid();
            var storageKey = $"{workspaceId:N}/{assignmentId:N}/{attachmentId:N}";
            var (stored, error) = await StoreAndScanAsync(storage, scanner, cleanupQueue, storageKey, request.Body, contentType, ct);
            if (error is not null) return error;
            try
            {
                var attachment = CourseAssignmentAttachment.Create(attachmentId, workspaceId, assignmentId,
                    null, null, CourseAssignmentAttachmentPurpose.TeacherMaterial, fileName, contentType,
                    stored!.SizeBytes, stored.Sha256, storageKey, userId, DateTimeOffset.UtcNow, assignment.DueAt);
                db.AssignmentAttachments.Add(attachment);
                AssignmentEndpoints.AddAudit(db, workspaceId, userId, attachmentId, "Assignment.MaterialUploaded");
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/assignments/{assignmentId}/attachments/{attachmentId}", ToView(attachment));
            }
            catch
            {
                await cleanupQueue.DeleteOrQueueAsync(storage, storageKey);
                throw;
            }
        });

        api.MapPost("/me/assignments/{assignmentId:guid}/attachments", async (
            Guid assignmentId, string fileName, HttpRequest request, ClaimsPrincipal user, CourseManagementDbContext db,
            ICourseAssignmentAttachmentStorage storage, ICourseAssignmentAttachmentScanner scanner,
            CourseAssignmentAttachmentCleanupQueue cleanupQueue, IPublishEndpoint publishEndpoint, CancellationToken ct) =>
        {
            var userId = AssignmentEndpoints.UserId(user);
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
            if (DateTimeOffset.UtcNow >= assignment.DueAt.AddDays(180))
                return Results.Conflict(new { code = "AssignmentAttachment.RetentionExpired" });
            var contentType = CourseAssignmentAttachmentPolicy.ContentTypeForFileName(fileName);
            var requestContentType = request.ContentType?.Split(';', 2)[0].Trim().ToLowerInvariant();
            if (contentType is null || !string.Equals(contentType, requestContentType, StringComparison.Ordinal))
                return Results.BadRequest(new { code = "AssignmentAttachment.UnsupportedFile" });
            if (request.ContentLength > CourseAssignmentAttachmentPolicy.MaxFileSizeBytes)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            var submission = await db.AssignmentSubmissions.SingleOrDefaultAsync(item => item.WorkspaceId == recipient.WorkspaceId
                && item.AssignmentId == assignmentId && item.StudentId == recipient.StudentId, ct);
            var createdSubmission = submission is null;
            var now = DateTimeOffset.UtcNow;
            if (createdSubmission)
            {
                submission = CourseAssignmentSubmission.Create(Guid.NewGuid(), recipient.WorkspaceId, assignmentId,
                    recipient.StudentId, null, now);
                db.AssignmentSubmissions.Add(submission);
            }
            else submission!.UpdateSubmission(submission.Text, now);

            var attachmentId = Guid.NewGuid();
            var storageKey = $"{recipient.WorkspaceId:N}/{assignmentId:N}/{attachmentId:N}";
            var (stored, error) = await StoreAndScanAsync(storage, scanner, cleanupQueue, storageKey, request.Body, contentType, ct);
            if (error is not null) return error;
            try
            {
                var attachment = CourseAssignmentAttachment.Create(attachmentId, recipient.WorkspaceId, assignmentId,
                    recipient.StudentId, submission!.Id, CourseAssignmentAttachmentPurpose.StudentSubmission,
                    fileName, contentType, stored!.SizeBytes, stored.Sha256, storageKey, userId, now, assignment.DueAt);
                db.AssignmentAttachments.Add(attachment);
                AssignmentEndpoints.AddAudit(db, recipient.WorkspaceId, userId, submission.Id,
                    createdSubmission ? "Assignment.FileSubmitted" : "Assignment.FileResubmitted");
                await publishEndpoint.Publish(new CourseManagementAssignmentSubmittedV1(assignmentId,
                    recipient.WorkspaceId, assignment.TeacherUserId), ct);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/course-management/v1/me/assignments/{assignmentId}/attachments/{attachmentId}", ToView(attachment));
            }
            catch
            {
                await cleanupQueue.DeleteOrQueueAsync(storage, storageKey);
                throw;
            }
        });

        api.MapGet("/workspaces/{workspaceId:guid}/assignments/{assignmentId:guid}/attachments/{attachmentId:guid}/content",
            async (Guid workspaceId, Guid assignmentId, Guid attachmentId, ClaimsPrincipal user, CourseManagementDbContext db,
                ICourseAssignmentAttachmentStorage storage, CancellationToken ct) =>
            {
                var assignment = await db.Assignments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == assignmentId
                    && item.WorkspaceId == workspaceId, ct);
                if (assignment is null || !await AssignmentEndpoints.CanManageAssignment(db, assignment,
                        AssignmentEndpoints.UserId(user), ct)) return Results.NotFound();
                var attachment = await db.AssignmentAttachments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == attachmentId
                    && item.WorkspaceId == workspaceId && item.AssignmentId == assignmentId
                    && item.ExpiresAt > DateTimeOffset.UtcNow, ct);
                if (attachment is null) return Results.NotFound();
                try { return Results.File(await storage.OpenReadAsync(attachment.StorageKey, ct), attachment.ContentType, attachment.FileName); }
                catch (FileNotFoundException) { return Results.NotFound(); }
            });

        api.MapGet("/me/assignments/{assignmentId:guid}/attachments/{attachmentId:guid}/content", async (
            Guid assignmentId, Guid attachmentId, ClaimsPrincipal user, CourseManagementDbContext db,
            ICourseAssignmentAttachmentStorage storage, CancellationToken ct) =>
        {
            var userId = AssignmentEndpoints.UserId(user);
            var studentId = await (from recipient in db.AssignmentRecipients.AsNoTracking()
                join student in db.Students.AsNoTracking() on recipient.StudentId equals student.Id
                join membership in db.Memberships.AsNoTracking() on recipient.WorkspaceId equals membership.WorkspaceId
                where recipient.AssignmentId == assignmentId && student.UserId == userId && student.IsActive
                    && membership.UserId == userId && membership.IsActive && membership.Role == WorkspaceRole.Student
                select (Guid?)student.Id).SingleOrDefaultAsync(ct);
            if (studentId is null) return Results.NotFound();
            var attachment = await db.AssignmentAttachments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == attachmentId
                && item.AssignmentId == assignmentId && item.ExpiresAt > DateTimeOffset.UtcNow
                && (item.Purpose == CourseAssignmentAttachmentPurpose.TeacherMaterial || item.StudentId == studentId), ct);
            if (attachment is null) return Results.NotFound();
            try { return Results.File(await storage.OpenReadAsync(attachment.StorageKey, ct), attachment.ContentType, attachment.FileName); }
            catch (FileNotFoundException) { return Results.NotFound(); }
        });
    }

    private static async Task<(StoredCourseAssignmentAttachment? Stored, IResult? Error)> StoreAndScanAsync(
        ICourseAssignmentAttachmentStorage storage, ICourseAssignmentAttachmentScanner scanner,
        CourseAssignmentAttachmentCleanupQueue cleanupQueue, string storageKey, Stream content, string contentType,
        CancellationToken cancellationToken)
    {
        StoredCourseAssignmentAttachment stored;
        try { stored = await storage.StoreAsync(storageKey, content, contentType, cancellationToken); }
        catch (ArgumentOutOfRangeException) { return (null, Results.StatusCode(StatusCodes.Status413PayloadTooLarge)); }
        catch (ArgumentException) { return (null, Results.BadRequest(new { code = "AssignmentAttachment.InvalidFile" })); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return (null, Results.StatusCode(StatusCodes.Status503ServiceUnavailable)); }

        CourseAssignmentAttachmentScanResult result;
        try
        {
            await using var stream = await storage.OpenReadAsync(storageKey, cancellationToken);
            result = await scanner.ScanAsync(stream, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await cleanupQueue.DeleteOrQueueAsync(storage, storageKey);
            throw;
        }
        catch
        {
            await cleanupQueue.DeleteOrQueueAsync(storage, storageKey);
            return (null, Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
        }

        if (result.IsClean) return (stored, null);
        await cleanupQueue.DeleteOrQueueAsync(storage, storageKey);
        return (null, Results.UnprocessableEntity(new { code = "AssignmentAttachment.Rejected" }));
    }

    internal static CourseAssignmentAttachmentView ToView(CourseAssignmentAttachment attachment) => new(attachment.Id,
        attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.Purpose.ToString(),
        attachment.UploadedAt, attachment.ExpiresAt);
}

public sealed record CourseAssignmentAttachmentView(Guid Id, string FileName, string ContentType, long SizeBytes,
    string Purpose, DateTimeOffset UploadedAt, DateTimeOffset ExpiresAt);
