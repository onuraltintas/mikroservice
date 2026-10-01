using Coaching.Application.Queries;

namespace Coaching.Application.Content;

public sealed record CoachingCmsEntrySummary(
    Guid Id,
    string Kind,
    string? Group,
    string Title,
    string Slug,
    string? Summary,
    string Content,
    string? SeoTitle,
    string? SeoDescription,
    IReadOnlyList<string> Tags,
    bool IsPublished,
    DateTime? ScheduledPublishAt,
    int SortOrder,
    int ViewCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int Version,
    string? Eyebrow = null,
    string? LinkLabel = null,
    string? LinkUrl = null,
    string? SecondaryLinkLabel = null,
    string? SecondaryLinkUrl = null,
    string? ImageUrl = null,
    string? Author = null,
    DateTime? PublishedAt = null,
    string? CoverImageUrl = null,
    bool TestimonialConsentConfirmed = false);

public sealed record CoachingCmsEntryRequest(
    string Kind,
    string? Group,
    string Title,
    string Slug,
    string? Summary,
    string Content,
    string? SeoTitle,
    string? SeoDescription,
    IReadOnlyList<string>? Tags,
    bool IsPublished,
    DateTime? ScheduledPublishAt,
    int SortOrder,
    string? Eyebrow = null,
    string? LinkLabel = null,
    string? LinkUrl = null,
    string? SecondaryLinkLabel = null,
    string? SecondaryLinkUrl = null,
    string? ImageUrl = null,
    string? Author = null,
    DateTime? PublishedAt = null,
    string? CoverImageUrl = null,
    bool TestimonialConsentConfirmed = false);

public sealed record CoachingCmsNavigationItemSummary(
    Guid Id,
    string Menu,
    string Label,
    string Url,
    string? Icon,
    int SortOrder,
    bool IsVisible,
    bool OpenInNewTab);

public sealed record CoachingCmsNavigationItemRequest(
    string Menu,
    string Label,
    string Url,
    string? Icon,
    int SortOrder,
    bool IsVisible,
    bool OpenInNewTab);

public sealed record CoachingCmsRevisionSummary(
    Guid Id,
    Guid EntryId,
    string Kind,
    int Version,
    DateTime CreatedAt,
    Guid CreatedBy);

public sealed record CoachingCmsMediaAssetSummary(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    string Url,
    string? AltText,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CoachingCmsMediaUpload(string FileName, string ContentType, long SizeBytes, Stream Content, string? AltText);
public sealed record CoachingCmsMediaDownload(Stream Content, string ContentType, string FileName);
public sealed record CoachingCmsStoredMedia(string StorageKey, long SizeBytes, string Sha256);

public interface ICoachingCmsMediaStorage
{
    Task<CoachingCmsStoredMedia> SaveAsync(Guid mediaId, CoachingCmsMediaUpload upload, string extension, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}

public static class CoachingCmsMediaPolicy
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public static string GetValidatedExtension(string contentType, string fileName, long sizeBytes)
    {
        if (sizeBytes <= 0 || sizeBytes > MaxFileSizeBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Görsel boyutu boş olamaz ve 10 MB sınırını aşamaz.");
        }

        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Length > 255
            || fileName.Contains('/')
            || fileName.Contains('\\')
            || fileName.Any(char.IsControl))
        {
            throw new ArgumentException("Görsel dosya adı geçersiz.", nameof(fileName));
        }

        var normalizedType = contentType.Trim().ToLowerInvariant();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var valid = normalizedType switch
        {
            "image/jpeg" when extension is ".jpg" or ".jpeg" => true,
            "image/png" when extension == ".png" => true,
            "image/webp" when extension == ".webp" => true,
            "image/gif" when extension == ".gif" => true,
            _ => false
        };
        if (!valid)
        {
            throw new ArgumentException("Yalnız JPEG, PNG, WebP veya GIF görselleri yüklenebilir.", nameof(contentType));
        }

        return extension;
    }

    public static bool HasValidSignature(string contentType, ReadOnlySpan<byte> header) => contentType.Trim().ToLowerInvariant() switch
    {
        "image/png" => header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/jpeg" => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
        "image/gif" => header.Length >= 4 && header[..4].SequenceEqual("GIF8"u8),
        "image/webp" => header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
        _ => false
    };
}

public interface ICoachingCms
{
    Task<PagedResponse<CoachingCmsEntrySummary>> GetEntriesAsync(
        string kind,
        int pageNumber,
        int pageSize,
        string? search,
        string? group = null,
        CancellationToken cancellationToken = default);
    Task<CoachingCmsEntrySummary?> GetEntryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid?> CreateEntryAsync(CoachingCmsEntryRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> UpdateEntryAsync(Guid id, CoachingCmsEntryRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> DeleteEntryAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CoachingCmsEntrySummary>> GetPublishedBlocksAsync(string group, CancellationToken cancellationToken = default);
    Task<CoachingCmsEntrySummary?> GetPublishedPageAsync(string slug, CancellationToken cancellationToken = default);
    Task<PagedResponse<CoachingCmsEntrySummary>> GetPublishedBlogAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<CoachingCmsEntrySummary?> GetPublishedBlogPostAsync(string slug, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CoachingCmsRevisionSummary>> GetRevisionsAsync(Guid entryId, CancellationToken cancellationToken = default);
    Task<bool> RestoreRevisionAsync(Guid entryId, Guid revisionId, Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CoachingCmsNavigationItemSummary>> GetNavigationAsync(string menu, bool includeHidden, CancellationToken cancellationToken = default);
    Task<Guid?> CreateNavigationItemAsync(CoachingCmsNavigationItemRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> UpdateNavigationItemAsync(Guid id, CoachingCmsNavigationItemRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> DeleteNavigationItemAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);
    Task<PagedResponse<CoachingCmsMediaAssetSummary>> GetMediaAssetsAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<CoachingCmsMediaAssetSummary> UploadMediaAsync(CoachingCmsMediaUpload upload, Guid actorId, CancellationToken cancellationToken = default);
    Task<CoachingCmsMediaDownload?> GetMediaDownloadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> DeleteMediaAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);
}
