using Coaching.Application.Attachments;
using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Commands.CreateAssignmentAttachment;

public sealed record CreateAssignmentAttachmentCommand(
    Guid AssignmentId,
    Guid StudentId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256) : IRequest<CreateAssignmentAttachmentResponse>;

public sealed record CreateAssignmentAttachmentResponse(
    Guid AssignmentId,
    Guid StudentId,
    Guid AttachmentId,
    string UploadUrl,
    DateTime UploadUrlExpiresAt,
    string Status);

public sealed class CreateAssignmentAttachmentCommandValidator
    : AbstractValidator<CreateAssignmentAttachmentCommand>
{
    public CreateAssignmentAttachmentCommandValidator()
    {
        RuleFor(command => command.AssignmentId).NotEmpty();
        RuleFor(command => command.StudentId).NotEmpty();
        RuleFor(command => command.FileName)
            .NotEmpty()
            .MaximumLength(255)
            .Must(name => !name.Contains('/') && !name.Contains('\\'))
            .WithMessage("File name cannot contain path separators.");
        RuleFor(command => command.ContentType)
            .Must(AssignmentAttachmentPolicy.IsSupportedContentType)
            .WithMessage("Only JPEG, PNG and WebP photos are supported.");
        RuleFor(command => command.SizeBytes)
            .InclusiveBetween(1, AssignmentAttachmentPolicy.MaxFileSizeBytes);
        RuleFor(command => command.Sha256)
            .Matches("^[0-9a-fA-F]{64}$")
            .WithMessage("Sha256 must be a 64-character hexadecimal value.");
    }
}

public sealed class CreateAssignmentAttachmentCommandHandler(
    IAssignmentRepository repository,
    IUnitOfWork unitOfWork,
    ICoachingAccessPolicy accessPolicy,
    IAssignmentAttachmentStorage storage)
    : IRequestHandler<CreateAssignmentAttachmentCommand, CreateAssignmentAttachmentResponse>
{
    public async Task<CreateAssignmentAttachmentResponse> Handle(
        CreateAssignmentAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        accessPolicy.RequireStudent(command.StudentId);
        AssignmentAttachmentPolicy.ValidateMetadata(
            command.FileName,
            command.ContentType,
            command.SizeBytes);

        var assignment = await repository.GetByIdAsync(command.AssignmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Assignment {command.AssignmentId} not found.");
        var studentAssignment = assignment.AssignedStudents
            .FirstOrDefault(student => student.StudentId == command.StudentId)
            ?? throw new InvalidOperationException("Student is not assigned to this assignment.");

        var storageKey = $"assignments/{assignment.Id:N}/students/{command.StudentId:N}/objects/{Guid.NewGuid():N}";
        var attachment = studentAssignment.AddSubmissionAttachment(
            storageKey,
            command.FileName.Trim(),
            command.ContentType.Trim().ToLowerInvariant(),
            command.SizeBytes,
            command.Sha256.ToUpperInvariant());

        var ticket = await storage.CreateUploadTicketAsync(
            assignment.Id,
            command.StudentId,
            attachment.Id,
            cancellationToken);
        attachment.SetUploadExpiry(ticket.ExpiresAt);

        await repository.UpdateAsync(assignment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateAssignmentAttachmentResponse(
            assignment.Id,
            command.StudentId,
            attachment.Id,
            ticket.UploadUrl,
            ticket.ExpiresAt,
            attachment.Status.ToString());
    }
}
