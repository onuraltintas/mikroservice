using EduPlatform.Shared.Kernel.Primitives;
using Coaching.Domain.Enums;

namespace Coaching.Domain.Entities;

/// <summary>
/// Öğrencinin ödev teslimine eklediği, nesne depolamadaki dosyanın metadata'sı.
/// Dosya içeriği bu aggregate veya PostgreSQL içinde tutulmaz.
/// </summary>
public sealed class AssignmentSubmissionAttachment : Entity
{
    public Guid AssignmentStudentId { get; private set; }
    public AssignmentStudent AssignmentStudent { get; private set; } = null!;

    public string StorageKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public AttachmentScanStatus Status { get; private set; }
    public DateTime? UploadExpiresAt { get; private set; }
    public DateTime? UploadedAt { get; private set; }
    public DateTime? ScannedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    private AssignmentSubmissionAttachment() { }

    internal static AssignmentSubmissionAttachment CreatePending(
        Guid assignmentStudentId,
        string storageKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256)
    {
        return new AssignmentSubmissionAttachment
        {
            AssignmentStudentId = assignmentStudentId,
            StorageKey = storageKey,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            Status = AttachmentScanStatus.PendingUpload,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkUploaded()
    {
        if (Status != AttachmentScanStatus.PendingUpload)
            throw new InvalidOperationException("Attachment is not awaiting upload.");

        if (!IsUploadWindowOpen(DateTime.UtcNow))
            throw new InvalidOperationException("Attachment upload window has expired.");

        UploadedAt = DateTime.UtcNow;
        Status = AttachmentScanStatus.PendingScan;
        UpdatedAt = UploadedAt;
    }

    public bool IsUploadWindowOpen(DateTime now)
    {
        return Status == AttachmentScanStatus.PendingUpload
            && UploadExpiresAt.HasValue
            && now < UploadExpiresAt.Value;
    }

    public void SetUploadExpiry(DateTime expiresAt)
    {
        if (Status != AttachmentScanStatus.PendingUpload)
            throw new InvalidOperationException("Attachment is not awaiting upload.");

        UploadExpiresAt = expiresAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkClean()
    {
        if (Status != AttachmentScanStatus.PendingScan)
            throw new InvalidOperationException("Attachment must be uploaded before it can be marked clean.");

        ScannedAt = DateTime.UtcNow;
        Status = AttachmentScanStatus.Clean;
        UpdatedAt = ScannedAt;
    }

    public void Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required.", nameof(reason));

        RejectionReason = reason.Trim();
        ScannedAt = DateTime.UtcNow;
        Status = AttachmentScanStatus.Rejected;
        UpdatedAt = ScannedAt;
    }
}
