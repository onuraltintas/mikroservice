using MediatR;

namespace Coaching.Application.Queries.GetAssignmentAttachment;

public sealed record GetAssignmentAttachmentQuery(
    Guid AssignmentId,
    Guid StudentId,
    Guid AttachmentId) : IRequest<AssignmentAttachmentDownload?>;

public sealed record AssignmentAttachmentDownload(
    Guid AttachmentId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTime? UploadedAt,
    Stream Content);
