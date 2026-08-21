using Coaching.Application.Attachments;

namespace Coaching.Infrastructure.Attachments;

/// <summary>
/// Development scanner. Content type, size, hash and image signatures are still
/// verified by the storage adapter; malware scanning is intentionally disabled.
/// Production configuration must select ClamAv.
/// </summary>
public sealed class DevelopmentAttachmentScanner : IAssignmentAttachmentScanner
{
    public Task<AttachmentScanResult> ScanAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AttachmentScanResult(IsClean: true));
    }
}
