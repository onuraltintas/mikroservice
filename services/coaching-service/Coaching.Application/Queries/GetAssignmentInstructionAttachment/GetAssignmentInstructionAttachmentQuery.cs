using MediatR;

namespace Coaching.Application.Queries.GetAssignmentInstructionAttachment;

public sealed record GetAssignmentInstructionAttachmentQuery(
    Guid AssignmentId,
    Guid AttachmentId) : IRequest<AssignmentInstructionAttachmentDownload?>;

public sealed record AssignmentInstructionAttachmentDownload(
    Guid AttachmentId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    Stream Content);
