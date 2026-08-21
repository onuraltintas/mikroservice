namespace Coaching.Application.Attachments;

public sealed class AssignmentAttachmentOptions
{
    public const string SectionName = "Coaching:Attachments";

    public string Provider { get; init; } = "Local";

    public string RootPath { get; init; } = Path.Combine(
        Path.GetTempPath(),
        "eduplatform-coaching-attachments");

    public int UploadUrlLifetimeMinutes { get; init; } = 15;
    public string MinioEndpoint { get; init; } = "minio:9000";
    public string MinioAccessKey { get; init; } = string.Empty;
    public string MinioSecretKey { get; init; } = string.Empty;
    public string MinioBucket { get; init; } = "eduplatform-attachments";
    public bool MinioUseSsl { get; init; }
}

public sealed class AttachmentScanOptions
{
    public const string SectionName = "Coaching:Attachments:Scanner";

    public string Provider { get; init; } = "Local";
    public string ClamAvHost { get; init; } = "clamav";
    public int ClamAvPort { get; init; } = 3310;
    public int TimeoutSeconds { get; init; } = 10;
}

public sealed record StoredAssignmentAttachment(long SizeBytes, string ContentType, string Sha256);
public sealed record AssignmentAttachmentUploadTicket(string UploadUrl, DateTime ExpiresAt);
public sealed record AttachmentScanResult(bool IsClean, string? ThreatName = null);

public interface IAssignmentAttachmentScanner
{
    Task<AttachmentScanResult> ScanAsync(
        Stream content,
        CancellationToken cancellationToken = default);
}

public interface IAssignmentAttachmentStorage
{
    Task<AssignmentAttachmentUploadTicket> CreateUploadTicketAsync(
        Guid assignmentId,
        Guid studentId,
        Guid attachmentId,
        CancellationToken cancellationToken = default);

    Task<StoredAssignmentAttachment> StoreAsync(
        string storageKey,
        Stream content,
        string expectedContentType,
        long expectedSizeBytes,
        string expectedSha256,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
