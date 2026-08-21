using System.Security.Cryptography;
using Minio;
using Minio.DataModel.Args;
using Coaching.Application.Attachments;
using Microsoft.Extensions.Options;

namespace Coaching.Infrastructure.Attachments;

/// <summary>
/// S3-compatible object storage adapter for horizontally scaled deployments.
/// The application still owns authorization and malware scanning; MinIO only
/// stores opaque, server-generated objects.
/// </summary>
public sealed class MinioAssignmentAttachmentStorage : IAssignmentAttachmentStorage, IDisposable
{
    private readonly AssignmentAttachmentOptions _options;
    private readonly IMinioClient _client;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private volatile bool _bucketReady;

    public MinioAssignmentAttachmentStorage(IOptions<AssignmentAttachmentOptions> options)
    {
        _options = options.Value;
        _client = new MinioClient()
            .WithEndpoint(_options.MinioEndpoint)
            .WithCredentials(_options.MinioAccessKey, _options.MinioSecretKey)
            .WithSSL(_options.MinioUseSsl)
            .Build();
    }

    public Task<AssignmentAttachmentUploadTicket> CreateUploadTicketAsync(
        Guid assignmentId,
        Guid studentId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
        ValidateStorageKey(storageKey);

        await using var buffer = new MemoryStream(capacity: checked((int)expectedSizeBytes));
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var readBuffer = new byte[64 * 1024];
        long total = 0;
        int read;
        while ((read = await content.ReadAsync(readBuffer.AsMemory(), cancellationToken)) > 0)
        {
            total += read;
            if (total > AssignmentAttachmentPolicy.MaxFileSizeBytes)
                throw new ArgumentOutOfRangeException(nameof(content), "Attachment is too large.");

            hash.AppendData(readBuffer, 0, read);
            await buffer.WriteAsync(readBuffer.AsMemory(0, read), cancellationToken);
        }

        if (total != expectedSizeBytes)
            throw new ArgumentException("Attachment size does not match the declared size.", nameof(content));

        var actualHash = Convert.ToHexString(hash.GetHashAndReset());
        if (!actualHash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Attachment hash does not match the declared hash.", nameof(content));

        ValidateImageSignature(buffer.GetBuffer().AsSpan(0, checked((int)total)), expectedContentType);
        await EnsureBucketAsync(cancellationToken);

        buffer.Position = 0;
        await _client.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(_options.MinioBucket)
                .WithObject(storageKey)
                .WithStreamData(buffer)
                .WithObjectSize(total)
                .WithContentType(expectedContentType.Trim().ToLowerInvariant()),
            cancellationToken);

        return new StoredAssignmentAttachment(
            total,
            expectedContentType.Trim().ToLowerInvariant(),
            actualHash);
    }

    public async Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        ValidateStorageKey(storageKey);
        await EnsureBucketAsync(cancellationToken);

        var output = new MemoryStream();
        try
        {
            await _client.GetObjectAsync(
                new GetObjectArgs()
                    .WithBucket(_options.MinioBucket)
                    .WithObject(storageKey)
                    .WithCallbackStream((input, token) => input.CopyToAsync(output, token)),
                cancellationToken);
            output.Position = 0;
            return output;
        }
        catch
        {
            await output.DisposeAsync();
            throw;
        }
    }

    public async Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        ValidateStorageKey(storageKey);
        await EnsureBucketAsync(cancellationToken);
        await _client.RemoveObjectAsync(
            new RemoveObjectArgs()
                .WithBucket(_options.MinioBucket)
                .WithObject(storageKey),
            cancellationToken);
    }

    public void Dispose()
    {
        _client.Dispose();
        _bucketLock.Dispose();
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (_bucketReady)
            return;

        await _bucketLock.WaitAsync(cancellationToken);
        try
        {
            if (_bucketReady)
                return;

            var exists = await _client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_options.MinioBucket),
                cancellationToken);
            if (!exists)
            {
                await _client.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(_options.MinioBucket),
                    cancellationToken);
            }

            _bucketReady = true;
        }
        finally
        {
            _bucketLock.Release();
        }
    }

    private static void ValidateStorageKey(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)
            || storageKey.Contains('\\')
            || storageKey.Contains("..", StringComparison.Ordinal)
            || storageKey.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Storage key is invalid.", nameof(storageKey));
        }
    }

    private static void ValidateImageSignature(ReadOnlySpan<byte> header, string contentType)
    {
        var normalized = contentType.Trim().ToLowerInvariant();
        var isValid = normalized switch
        {
            "image/jpeg" => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/png" => header.Length >= 8
                && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => header.Length >= 12
                && header[..4].SequenceEqual("RIFF"u8)
                && header[8..12].SequenceEqual("WEBP"u8),
            _ => false
        };

        if (!isValid)
            throw new ArgumentException("Attachment content does not match its declared image type.", nameof(contentType));
    }
}
