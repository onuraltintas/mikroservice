using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SpeedReading.Application.Content;

namespace SpeedReading.Infrastructure.ExternalServices;

public sealed class LocalCmsMediaStorage : ISpeedReadingCmsMediaStorage
{
    private readonly string rootPath;
    private readonly ILogger<LocalCmsMediaStorage> logger;

    public LocalCmsMediaStorage(IConfiguration configuration, ILogger<LocalCmsMediaStorage> logger)
    {
        var configuredPath = configuration["SpeedReading:CmsMedia:RootPath"]
            ?? Environment.GetEnvironmentVariable("SPEED_READING_CMS_MEDIA_ROOT");
        rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "media")
            : configuredPath);
        this.logger = logger;
    }

    public async Task<CmsStoredMedia> SaveAsync(
        Guid mediaId,
        CmsMediaUpload upload,
        string extension,
        CancellationToken cancellationToken = default)
    {
        var storageKey = $"cms/{mediaId:N}{extension}";
        var finalPath = ResolvePath(storageKey);
        var directory = Path.GetDirectoryName(finalPath)
            ?? throw new InvalidOperationException("CMS media storage directory is invalid.");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{finalPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            long bytesWritten = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[64 * 1024];
                int read;
                while ((read = await upload.Content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    hash.AppendData(buffer, 0, read);
                    bytesWritten += read;
                    if (bytesWritten > CmsMediaPolicy.MaxFileSizeBytes)
                    {
                        throw new ArgumentOutOfRangeException(nameof(upload), "The image exceeds the maximum size.");
                    }
                }
            }

            if (bytesWritten != upload.SizeBytes)
            {
                throw new InvalidDataException("The uploaded image size does not match its metadata.");
            }

            var header = new byte[12];
            await using (var input = new FileStream(
                temporaryPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                header.Length,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var headerLength = await input.ReadAsync(header, cancellationToken);
                if (!CmsMediaPolicy.HasValidSignature(upload.ContentType, header.AsSpan(0, headerLength)))
                {
                    throw new InvalidDataException("The uploaded image signature does not match its content type.");
                }
            }

            File.Move(temporaryPath, finalPath, overwrite: true);
            return new CmsStoredMedia(
                storageKey,
                bytesWritten,
                Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(storageKey);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TryDelete(ResolvePath(storageKey));
        return Task.CompletedTask;
    }

    private string ResolvePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || Path.IsPathRooted(storageKey))
        {
            throw new InvalidOperationException("CMS media storage key is invalid.");
        }

        var normalizedKey = storageKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, normalizedKey));
        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("CMS media storage key escapes the storage root.");
        }

        return fullPath;
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not remove CMS media file {Path}", path);
        }
    }
}
