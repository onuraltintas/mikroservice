namespace CourseManagement.Domain;

public enum CourseAssignmentAttachmentPurpose
{
    TeacherMaterial = 1,
    StudentSubmission = 2
}

public sealed class CourseAssignmentAttachment
{
    private CourseAssignmentAttachment() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid? StudentId { get; private set; }
    public Guid? SubmissionId { get; private set; }
    public CourseAssignmentAttachmentPurpose Purpose { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public void AnonymizeUploader(Guid anonymizedUserId)
    {
        if (anonymizedUserId == Guid.Empty) throw new ArgumentException("Anonymized uploader identifier is required.", nameof(anonymizedUserId));
        UploadedByUserId = anonymizedUserId;
    }

    public static CourseAssignmentAttachment Create(Guid id, Guid workspaceId, Guid assignmentId,
        Guid? studentId, Guid? submissionId, CourseAssignmentAttachmentPurpose purpose, string fileName,
        string contentType, long sizeBytes, string sha256, string storageKey, Guid uploadedByUserId,
        DateTimeOffset uploadedAt, DateTimeOffset dueAt)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || assignmentId == Guid.Empty || uploadedByUserId == Guid.Empty)
            throw new ArgumentException("Assignment attachment identifiers are required.");
        if (!Enum.IsDefined(purpose)) throw new ArgumentOutOfRangeException(nameof(purpose));
        if ((purpose == CourseAssignmentAttachmentPurpose.StudentSubmission) != (studentId is not null && submissionId is not null)
            || studentId == Guid.Empty || submissionId == Guid.Empty)
            throw new ArgumentException("Student submission attachments require both student and submission identifiers.");
        if (purpose == CourseAssignmentAttachmentPurpose.TeacherMaterial && (studentId is not null || submissionId is not null))
            throw new ArgumentException("Teacher materials cannot be linked to a student submission.");
        if (sizeBytes is < 1 or > 10 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Attachment must be between 1 byte and 10 MB.");
        if (dueAt == default || uploadedAt == default) throw new ArgumentException("Attachment dates are required.");

        var normalizedName = Path.GetFileName(fileName.Replace('\\', '/').Trim());
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 180
            || normalizedName.Any(char.IsControl) || normalizedName is "." or "..")
            throw new ArgumentException("Attachment file name is invalid.", nameof(fileName));
        var normalizedType = contentType.Trim().ToLowerInvariant();
        if (normalizedType is not ("application/pdf" or "image/jpeg" or "image/png"))
            throw new ArgumentException("Attachment file type is not supported.", nameof(contentType));
        if (!System.Text.RegularExpressions.Regex.IsMatch(sha256, "\\A[0-9A-Fa-f]{64}\\z"))
            throw new ArgumentException("Attachment hash is invalid.", nameof(sha256));
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Length > 300 || storageKey.Contains("..", StringComparison.Ordinal)
            || storageKey.Contains('\\'))
            throw new ArgumentException("Attachment storage key is invalid.", nameof(storageKey));

        return new CourseAssignmentAttachment
        {
            Id = id,
            WorkspaceId = workspaceId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            SubmissionId = submissionId,
            Purpose = purpose,
            FileName = normalizedName,
            ContentType = normalizedType,
            SizeBytes = sizeBytes,
            Sha256 = sha256.ToUpperInvariant(),
            StorageKey = storageKey,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = uploadedAt.ToUniversalTime(),
            ExpiresAt = dueAt.ToUniversalTime().AddDays(180)
        };
    }
}
