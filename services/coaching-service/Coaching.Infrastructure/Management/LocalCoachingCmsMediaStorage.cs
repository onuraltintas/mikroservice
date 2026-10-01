using System.Security.Cryptography;
using Coaching.Application.Content;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Coaching.Infrastructure.Management;

public sealed class LocalCoachingCmsMediaStorage : ICoachingCmsMediaStorage
{
    private readonly string rootPath;
    private readonly ILogger<LocalCoachingCmsMediaStorage> logger;

    public LocalCoachingCmsMediaStorage(IConfiguration configuration, ILogger<LocalCoachingCmsMediaStorage> logger)
    {
        var configuredPath = configuration["Coaching:CmsMedia:RootPath"]
            ?? Environment.GetEnvironmentVariable("COACHING_CMS_MEDIA_ROOT");
        rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "media", "coaching-cms")
            : configuredPath);
        this.logger = logger;
    }

    public async Task<CoachingCmsStoredMedia> SaveAsync(
        Guid mediaId,
        CoachingCmsMediaUpload upload,
        string extension,
        CancellationToken cancellationToken = default)
    {
        var storageKey = $"cms/{mediaId:N}{extension}";
        var finalPath = ResolvePath(storageKey);
        var directory = Path.GetDirectoryName(finalPath)
            ?? throw new InvalidOperationException("Koçluk CMS dosya yolu geçersiz.");
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
                    if (bytesWritten > CoachingCmsMediaPolicy.MaxFileSizeBytes)
                    {
                        throw new InvalidDataException("Görsel 10 MB boyut sınırını aşıyor.");
                    }
                }
            }

            if (bytesWritten != upload.SizeBytes)
            {
                throw new InvalidDataException("Yüklenen görselin boyutu doğrulanamadı.");
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
                if (!CoachingCmsMediaPolicy.HasValidSignature(upload.ContentType, header.AsSpan(0, headerLength)))
                {
                    throw new InvalidDataException("Dosyanın içeriği seçilen görsel türüyle eşleşmiyor.");
                }
            }

            File.Move(temporaryPath, finalPath, overwrite: true);
            return new CoachingCmsStoredMedia(
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
            throw new InvalidOperationException("Koçluk CMS dosya anahtarı geçersiz.");
        }

        var normalizedKey = storageKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, normalizedKey));
        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar) ? rootPath : rootPath + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Koçluk CMS dosya yolu depolama kökünün dışına çıkamaz.");
        }

        return fullPath;
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Koçluk CMS dosyası silinemedi: {Path}", path);
        }
    }
}
