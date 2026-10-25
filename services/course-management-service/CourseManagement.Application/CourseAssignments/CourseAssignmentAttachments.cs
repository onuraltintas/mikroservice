namespace CourseManagement.Application.CourseAssignments;

public sealed class CourseAssignmentAttachmentStorageOptions
{
    public const string SectionName = "CourseManagement:AssignmentAttachments";

    public string RootPath { get; init; } = Path.Combine(Path.GetTempPath(), "eduplatform-course-management-assignment-attachments");
    public string ScannerProvider { get; init; } = "Development";
    public string ClamAvHost { get; init; } = "clamav";
    public int ClamAvPort { get; init; } = 3310;
    public int ScannerTimeoutSeconds { get; init; } = 10;
}

public sealed record StoredCourseAssignmentAttachment(long SizeBytes, string Sha256);
public sealed record CourseAssignmentAttachmentScanResult(bool IsClean, string? ThreatName = null);

public interface ICourseAssignmentAttachmentStorage
{
    Task<StoredCourseAssignmentAttachment> StoreAsync(string storageKey, Stream content, string contentType,
        CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}

public interface ICourseAssignmentAttachmentScanner
{
    Task<CourseAssignmentAttachmentScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default);
}

public static class CourseAssignmentAttachmentPolicy
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public static string? ContentTypeForFileName(string fileName) => Path.GetExtension(fileName.Replace('\\', '/').Trim()).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => null
    };

    public static bool IsValidContentType(string? contentType) => contentType is
        "application/pdf" or "image/jpeg" or "image/png";
}
