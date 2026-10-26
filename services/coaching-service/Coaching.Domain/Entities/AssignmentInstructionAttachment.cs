using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

/// <summary>
/// Teacher-provided assignment material stored outside the database.
/// </summary>
public sealed class AssignmentInstructionAttachment : Entity
{
    public Guid AssignmentId { get; private set; }
    public Assignment Assignment { get; private set; } = null!;
    public string StorageKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public AttachmentScanStatus Status { get; private set; }
    public DateTime UploadedAt { get; private set; }
    public DateTime ScannedAt { get; private set; }

    private AssignmentInstructionAttachment() { }

    internal static AssignmentInstructionAttachment CreateClean(
        Guid assignmentId,
        string storageKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        DateTime uploadedAt)
    {
        return new AssignmentInstructionAttachment
        {
            AssignmentId = assignmentId,
            StorageKey = storageKey,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            Status = AttachmentScanStatus.Clean,
            UploadedAt = uploadedAt,
            ScannedAt = DateTime.UtcNow,
            CreatedAt = uploadedAt
        };
    }
}
