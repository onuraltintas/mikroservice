namespace Coaching.Application.Attachments;

public sealed record AssignmentAttachmentDeletionRequested(
    Guid AssignmentId,
    Guid AttachmentId,
    string StorageKey);
