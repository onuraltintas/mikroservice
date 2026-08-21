using Coaching.Application.Attachments;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Kernel.Exceptions;
using MediatR;

namespace Coaching.Application.Commands.UploadAssignmentAttachment;

public sealed record UploadAssignmentAttachmentCommand(
    Guid AssignmentId,
    Guid StudentId,
    Guid AttachmentId,
    Stream Content,
    string ContentType,
    long SizeBytes,
    string Sha256) : IRequest<UploadAssignmentAttachmentResponse>;

public sealed record UploadAssignmentAttachmentResponse(
    Guid AttachmentId,
    long SizeBytes,
    string Sha256,
    string Status,
    DateTime UploadedAt);

public sealed class UploadAssignmentAttachmentCommandHandler(
    IAssignmentRepository repository,
    IUnitOfWork unitOfWork,
    ICoachingAccessPolicy accessPolicy,
    IAssignmentAttachmentStorage storage,
    IAssignmentAttachmentScanner scanner)
    : IRequestHandler<UploadAssignmentAttachmentCommand, UploadAssignmentAttachmentResponse>
{
    public async Task<UploadAssignmentAttachmentResponse> Handle(
        UploadAssignmentAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        accessPolicy.RequireStudent(command.StudentId);
        AssignmentAttachmentPolicy.ValidateContentMetadata(command.ContentType, command.SizeBytes);

        var assignment = await repository.GetByIdAsync(command.AssignmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Assignment {command.AssignmentId} not found.");
        var studentAssignment = assignment.AssignedStudents
            .FirstOrDefault(student => student.StudentId == command.StudentId)
            ?? throw new InvalidOperationException("Student is not assigned to this assignment.");
        var attachment = studentAssignment.SubmissionAttachments
            .FirstOrDefault(item => item.Id == command.AttachmentId)
            ?? throw new InvalidOperationException("Attachment not found.");

        if (!attachment.IsUploadWindowOpen(DateTime.UtcNow))
        {
            throw new BusinessRuleException(
                "Attachment.UploadExpired",
                "Attachment upload window has expired.");
        }

        if (!string.Equals(attachment.ContentType, command.ContentType.Trim(), StringComparison.OrdinalIgnoreCase)
            || attachment.SizeBytes != command.SizeBytes
            || !attachment.Sha256.Equals(command.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "Attachment.MetadataMismatch",
                "Uploaded content does not match the attachment metadata.");
        }

        var stored = await storage.StoreAsync(
            attachment.StorageKey,
            command.Content,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.Sha256,
            cancellationToken);

        attachment.MarkUploaded();
        await repository.UpdateAsync(assignment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await using var storedContent = await storage.OpenReadAsync(
            attachment.StorageKey,
            cancellationToken);
        var scanResult = await scanner.ScanAsync(storedContent, cancellationToken);
        if (!scanResult.IsClean)
        {
            attachment.Reject(scanResult.ThreatName ?? "Attachment rejected by malware scanner.");
            await repository.UpdateAsync(assignment, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await storage.DeleteAsync(attachment.StorageKey, cancellationToken);

            throw new BusinessRuleException(
                "Attachment.Rejected",
                "Attachment failed the security scan and was rejected.");
        }

        attachment.MarkClean();
        await repository.UpdateAsync(assignment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UploadAssignmentAttachmentResponse(
            attachment.Id,
            stored.SizeBytes,
            stored.Sha256,
            attachment.Status.ToString(),
            attachment.UploadedAt!.Value);
    }
}
