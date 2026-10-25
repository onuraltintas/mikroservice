using System.Security.Cryptography;
using CourseManagement.Application.CourseAssignments;
using Microsoft.Extensions.Options;

namespace CourseManagement.Infrastructure.Attachments;

public sealed class LocalCourseAssignmentAttachmentStorage(IOptions<CourseAssignmentAttachmentStorageOptions> options)
    : ICourseAssignmentAttachmentStorage
{
    private readonly string _rootPath = Path.GetFullPath(options.Value.RootPath);

    public async Task<StoredCourseAssignmentAttachment> StoreAsync(string storageKey, Stream content, string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!CourseAssignmentAttachmentPolicy.IsValidContentType(contentType))
            throw new ArgumentException("Attachment content type is not supported.", nameof(contentType));

        var finalPath = GetSafePath(storageKey);
        var directory = Path.GetDirectoryName(finalPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(finalPath)}.{Guid.NewGuid():N}.part");
        try
        {
            long total = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 64 * 1024, useAsync: true))
            {
                var buffer = new byte[64 * 1024];
                int read;
                while ((read = await content.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
                {
                    total += read;
                    if (total > CourseAssignmentAttachmentPolicy.MaxFileSizeBytes)
                        throw new ArgumentOutOfRangeException(nameof(content), "Attachment exceeds 10 MB.");
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            if (total == 0) throw new ArgumentException("Attachment cannot be empty.", nameof(content));
            await ValidateSignatureAsync(temporaryPath, contentType, cancellationToken);
            File.Move(temporaryPath, finalPath, overwrite: false);
            return new StoredCourseAssignmentAttachment(total, Convert.ToHexString(hash.GetHashAndReset()));
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(GetSafePath(storageKey), FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetSafePath(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetSafePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Contains('\\')
            || storageKey.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(storageKey))
            throw new ArgumentException("Attachment storage key is invalid.", nameof(storageKey));
        var path = Path.GetFullPath(Path.Combine(_rootPath, storageKey.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = _rootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Attachment storage key escapes the storage root.", nameof(storageKey));
        return path;
    }

    private static async Task ValidateSignatureAsync(string path, string contentType, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        var header = new byte[8];
        var read = await input.ReadAsync(header.AsMemory(), cancellationToken);
        var valid = contentType switch
        {
            "application/pdf" => read >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
            "image/jpeg" => read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/png" => read >= 8 && header.AsSpan().SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            _ => false
        };
        if (!valid) throw new ArgumentException("File content does not match the declared file type.", nameof(contentType));
    }
}
