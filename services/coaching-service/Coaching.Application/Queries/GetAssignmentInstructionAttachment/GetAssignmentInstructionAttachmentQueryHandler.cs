using Coaching.Application.Attachments;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;
using MediatR;

namespace Coaching.Application.Queries.GetAssignmentInstructionAttachment;

public sealed class GetAssignmentInstructionAttachmentQueryHandler(
    IAssignmentRepository repository,
    ICoachingAccessPolicy accessPolicy,
    ICoachingIdentityAuthorizationClient identityAuthorizationClient,
    IAssignmentAttachmentStorage storage)
    : IRequestHandler<GetAssignmentInstructionAttachmentQuery, AssignmentInstructionAttachmentDownload?>
{
    public async Task<AssignmentInstructionAttachmentDownload?> Handle(
        GetAssignmentInstructionAttachmentQuery query,
        CancellationToken cancellationToken)
    {
        var assignment = await repository.GetByIdAsync(query.AssignmentId, cancellationToken);
        if (assignment is null) return null;

        var attachment = assignment.InstructionAttachments.SingleOrDefault(item => item.Id == query.AttachmentId);
        if (attachment is null) return null;
        if (attachment.Status != AttachmentScanStatus.Clean)
            throw new BusinessRuleException("Attachment.NotAvailable", "Dosya güvenlik taraması tamamlanmadan indirilemez.");

        if (accessPolicy.CurrentUserId == assignment.TeacherId)
        {
            accessPolicy.RequireTeacher(assignment.TeacherId);
        }
        else
        {
            var allowedStudentIds = await CoachingStudentReadAuthorization.RequireWorkAsync(
            accessPolicy,
            identityAuthorizationClient,
            assignment.AssignedStudents.Select(student => student.StudentId).ToArray(),
            cancellationToken);
            if (allowedStudentIds.Count == 0)
                throw new BusinessRuleException("Authorization.Forbidden", "Bu ödev dosyasına erişim yetkiniz yok.");
        }

        var content = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        return new AssignmentInstructionAttachmentDownload(
            attachment.Id,
            attachment.OriginalFileName,
            attachment.ContentType,
            attachment.SizeBytes,
            content);
    }
}
