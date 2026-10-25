using System.Security.Cryptography;
using Coaching.Application.Attachments;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Kernel.Exceptions;
using MediatR;

namespace Coaching.Application.Commands.UploadAssignmentInstructionAttachment;

public sealed record UploadAssignmentInstructionAttachmentCommand(
    Guid AssignmentId,
    string FileName,
    string ContentType,
    byte[] Content) : IRequest<AssignmentInstructionAttachmentResponse>;

public sealed record AssignmentInstructionAttachmentResponse(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Status,
    DateTime UploadedAt,
    DateTime ScannedAt);

public sealed class UploadAssignmentInstructionAttachmentCommandHandler(
    IAssignmentRepository repository,
    IUnitOfWork unitOfWork,
    ICoachingAccessPolicy accessPolicy,
    IAssignmentAttachmentStorage storage,
    IAssignmentAttachmentScanner scanner)
    : IRequestHandler<UploadAssignmentInstructionAttachmentCommand, AssignmentInstructionAttachmentResponse>
{
    public async Task<AssignmentInstructionAttachmentResponse> Handle(
        UploadAssignmentInstructionAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        if (accessPolicy.CurrentUserId is not { } teacherId)
            throw new BusinessRuleException("Authorization.Forbidden", "Teacher access is required.");

        var assignment = await repository.GetByIdAsync(command.AssignmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Assignment {command.AssignmentId} not found.");
        accessPolicy.RequireTeacher(assignment.TeacherId);
        if (teacherId != assignment.TeacherId)
            throw new BusinessRuleException("Authorization.Forbidden", "Only the assignment owner can attach files.");

        var sizeBytes = command.Content.LongLength;
        AssignmentAttachmentPolicy.ValidateMetadata(command.FileName, command.ContentType, sizeBytes);
        var sha256 = Convert.ToHexString(SHA256.HashData(command.Content));
        var storageKey = $"assignments/{assignment.Id:N}/instructions/{Guid.NewGuid():N}";

        await using (var content = new MemoryStream(command.Content, writable: false))
        {
            await storage.StoreAsync(storageKey, content, command.ContentType, sizeBytes, sha256, cancellationToken);
        }

        try
        {
            await using var storedContent = await storage.OpenReadAsync(storageKey, cancellationToken);
            var scanResult = await scanner.ScanAsync(storedContent, cancellationToken);
            if (!scanResult.IsClean)
                throw new BusinessRuleException("Attachment.Rejected", "Dosya güvenlik taramasını geçemedi.");

            var uploadedAt = DateTime.UtcNow;
            var attachment = assignment.AddInstructionAttachment(
                storageKey,
                Path.GetFileName(command.FileName.Trim()),
                command.ContentType.Trim().ToLowerInvariant(),
                sizeBytes,
                sha256,
                uploadedAt);
            await repository.UpdateAsync(assignment, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new AssignmentInstructionAttachmentResponse(
                attachment.Id,
                attachment.OriginalFileName,
                attachment.ContentType,
                attachment.SizeBytes,
                attachment.Status.ToString(),
                attachment.UploadedAt,
                attachment.ScannedAt);
        }
        catch
        {
            await storage.DeleteAsync(storageKey, cancellationToken);
            throw;
        }
    }
}
