using System.Security.Cryptography;
using Coaching.Application.Attachments;
using Microsoft.Extensions.Options;

namespace Coaching.Infrastructure.Attachments;

/// <summary>
/// Development-only storage adapter. Production must use an object-storage adapter
/// (MinIO/S3-compatible) behind the same application interface.
/// </summary>
public sealed class LocalAssignmentAttachmentStorage(
    IOptions<AssignmentAttachmentOptions> options) : IAssignmentAttachmentStorage
{
    private readonly AssignmentAttachmentOptions _options = options.Value;
    private readonly string _rootPath = Path.GetFullPath(options.Value.RootPath);

    public Task<AssignmentAttachmentUploadTicket> CreateUploadTicketAsync(
        Guid assignmentId,
        Guid studentId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.UploadUrlLifetimeMinutes);
        return Task.FromResult(new AssignmentAttachmentUploadTicket(
            $"/api/assignments/{assignmentId}/students/{studentId}/attachments/{attachmentId}/content",
            expiresAt));
    }

    public async Task<StoredAssignmentAttachment> StoreAsync(
        string storageKey,
        Stream content,
        string expectedContentType,
        long expectedSizeBytes,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        AssignmentAttachmentPolicy.ValidateContentMetadata(expectedContentType, expectedSizeBytes);

        var finalPath = GetSafePath(storageKey);
        var directory = Path.GetDirectoryName(finalPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(finalPath)}.{Guid.NewGuid():N}.part");

        try
        {
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                useAsync: true))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[64 * 1024];
                long total = 0;
                int read;
                while ((read = await content.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
                {
                    total += read;
                    if (total > AssignmentAttachmentPolicy.MaxFileSizeBytes)
                        throw new ArgumentOutOfRangeException(nameof(content), "Attachment is too large.");

                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }

                if (total != expectedSizeBytes)
                    throw new ArgumentException("Attachment size does not match the declared size.", nameof(content));

                var actualHash = Convert.ToHexString(hash.GetHashAndReset());
                if (!actualHash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Attachment hash does not match the declared hash.", nameof(content));
            }

            await ValidateContentSignatureAsync(temporaryPath, expectedContentType, cancellationToken);
            File.Move(temporaryPath, finalPath, overwrite: false);

            return new StoredAssignmentAttachment(
                expectedSizeBytes,
                expectedContentType.Trim().ToLowerInvariant(),
                expectedSha256.ToUpperInvariant());
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetSafePath(storageKey);
        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetSafePath(storageKey);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    private string GetSafePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)
            || storageKey.Contains('\\')
            || storageKey.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Storage key is invalid.", nameof(storageKey));
        }

        var path = Path.GetFullPath(Path.Combine(
            _rootPath,
            storageKey.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = _rootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Storage key escapes the storage root.", nameof(storageKey));

        return path;
    }

    private static async Task ValidateContentSignatureAsync(
        string path,
        string expectedContentType,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        var header = new byte[12];
        var read = await input.ReadAsync(header.AsMemory(), cancellationToken);

        var isValid = expectedContentType.Trim().ToLowerInvariant() switch
        {
            "image/jpeg" => read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/png" => read >= 8
                && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => read >= 12
                && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };

        if (!isValid)
            throw new ArgumentException("Attachment content does not match its declared image type.", nameof(expectedContentType));
    }
}
