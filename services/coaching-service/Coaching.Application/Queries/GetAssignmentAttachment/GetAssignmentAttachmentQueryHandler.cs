using Coaching.Application.Authorization;
using Coaching.Application.Attachments;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Kernel.Exceptions;
using MediatR;

namespace Coaching.Application.Queries.GetAssignmentAttachment;

public sealed class GetAssignmentAttachmentQueryHandler(
    IAssignmentRepository repository,
    ICoachingAccessPolicy accessPolicy,
    ICoachingIdentityAuthorizationClient identityAuthorizationClient,
    IAssignmentAttachmentStorage storage)
    : IRequestHandler<GetAssignmentAttachmentQuery, AssignmentAttachmentDownload?>
{
    public async Task<AssignmentAttachmentDownload?> Handle(
        GetAssignmentAttachmentQuery query,
        CancellationToken cancellationToken)
    {
        var assignment = await repository.GetByIdAsync(query.AssignmentId, cancellationToken);
        if (assignment is null)
            return null;

        var studentAssignment = assignment.AssignedStudents
            .FirstOrDefault(student => student.StudentId == query.StudentId);
        if (studentAssignment is null)
            return null;

        var attachment = studentAssignment.SubmissionAttachments
            .FirstOrDefault(item => item.Id == query.AttachmentId);
        if (attachment is null)
            return null;

        var allowedStudentIds = await CoachingStudentReadAuthorization.RequireAsync(
            accessPolicy,
            identityAuthorizationClient,
            [query.StudentId],
            cancellationToken);
        if (!allowedStudentIds.Contains(query.StudentId))
        {
            throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Bu attachment verisine erişim yetkiniz yok.");
        }

        if (attachment.Status != Domain.Enums.AttachmentScanStatus.Clean)
        {
            throw new BusinessRuleException(
                "Attachment.NotAvailable",
                "Attachment güvenlik taraması tamamlanmadan indirilemez.");
        }

        var content = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        return new AssignmentAttachmentDownload(
            attachment.Id,
            attachment.OriginalFileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.UploadedAt,
            content);
    }
}
