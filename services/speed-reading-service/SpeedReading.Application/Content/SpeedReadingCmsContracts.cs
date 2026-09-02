namespace SpeedReading.Application.Content;

public sealed record CmsContentBlockSummary(
    Guid Id,
    string Key,
    string Group,
    string? Label,
    int Type,
    string Value);

public sealed record CmsSeoSettings(
    string? MetaTitle,
    string? MetaDescription,
    string? MetaKeywords,
    string? CanonicalUrl,
    string? OgTitle,
    string? OgDescription,
    string? OgImage,
    bool NoIndex);

public sealed record CmsPageSummary(
    Guid Id,
    string Title,
    string Slug,
    string Content,
    bool IsPublished,
    CmsSeoSettings SeoSettings,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CmsBlogPostSummary(
    Guid Id,
    string Title,
    string Slug,
    string? Summary,
    string Content,
    string? Author,
    DateTime? PublishedAt,
    IReadOnlyList<string> Tags,
    string? CoverImageUrl,
    CmsSeoSettings SeoSettings,
    int ViewCount,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CmsContactMessageSummary(
    Guid Id,
    string Name,
    string Email,
    string Subject,
    string Message,
    bool IsRead,
    bool IsReplied,
    DateTime? RepliedAt,
    string? ReplyContent,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CmsNewsletterSubscriberSummary(
    Guid Id,
    string Email,
    bool IsActive,
    string? Source,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CmsPageRequest(
    string Title,
    string Slug,
    string Content,
    bool IsPublished,
    CmsSeoSettings SeoSettings);

public sealed record CmsBlogPostRequest(
    string Title,
    string Slug,
    string? Summary,
    string Content,
    string? Author,
    DateTime? PublishedAt,
    IReadOnlyList<string>? Tags,
    string? CoverImageUrl,
    bool IsPublished,
    CmsSeoSettings SeoSettings);

public sealed record CmsContentBlockRequest(
    string Key,
    string Group,
    string? Label,
    int Type,
    string Value);

public sealed record CmsContactMessageRequest(
    string Name,
    string Email,
    string Subject,
    string Message);

public sealed record CmsNewsletterSubscriptionRequest(string Email, string? Name);

public sealed record CmsNewsletterUnsubscribeRequest(string Token);

public static class SpeedReadingNewsletterRules
{
    public static bool TryGetSubscriberId(string? token, out Guid subscriberId) =>
        Guid.TryParse(token?.Trim(), out subscriberId) && subscriberId != Guid.Empty;
}

public sealed record CmsContactReplyRequest(Guid MessageId, string ReplyContent);

public sealed record CmsMediaAssetSummary(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    string Url,
    string? AltText,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CmsMediaUpload(
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content,
    string? AltText);

public sealed record CmsMediaDownload(
    Stream Content,
    string ContentType,
    string FileName);

public sealed record CmsStoredMedia(
    string StorageKey,
    long SizeBytes,
    string Sha256);

public sealed record CmsNavigationItemSummary(
    Guid Id,
    string Menu,
    string Label,
    string Url,
    string? Fragment,
    string? Icon,
    int SortOrder,
    bool IsVisible,
    bool OpenInNewTab,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CmsNavigationItemRequest(
    string Menu,
    string Label,
    string Url,
    string? Fragment,
    string? Icon,
    int SortOrder,
    bool IsVisible,
    bool OpenInNewTab);

public static class CmsNavigationRules
{
    public static CmsNavigationItemRequest Normalize(CmsNavigationItemRequest request)
    {
        var menu = string.IsNullOrWhiteSpace(request.Menu) ? "Main" : request.Menu.Trim();
        var label = request.Label?.Trim() ?? string.Empty;
        var url = request.Url?.Trim() ?? string.Empty;

        if (menu.Length is < 1 or > 50)
        {
            throw new ArgumentException("The menu name must be between 1 and 50 characters.", nameof(request));
        }

        if (label.Length is < 1 or > 100)
        {
            throw new ArgumentException("The navigation label must be between 1 and 100 characters.", nameof(request));
        }

        if (url.Length is < 1 or > 500 || !IsSafeUrl(url))
        {
            throw new ArgumentException("The navigation URL must be an internal path or an HTTP(S) URL.", nameof(request));
        }

        var fragment = string.IsNullOrWhiteSpace(request.Fragment)
            ? null
            : request.Fragment.Trim().TrimStart('#');
        if (fragment is not null && (fragment.Length > 100 || fragment.Any(character => !IsFragmentCharacter(character))))
        {
            throw new ArgumentException("The navigation fragment contains invalid characters.", nameof(request));
        }

        var icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim();
        if (icon is not null && icon.Length > 50)
        {
            throw new ArgumentException("The navigation icon is too long.", nameof(request));
        }

        return request with
        {
            Menu = menu,
            Label = label,
            Url = url,
            Fragment = fragment,
            Icon = icon,
            SortOrder = Math.Max(0, request.SortOrder)
        };
    }

    private static bool IsSafeUrl(string url) =>
        (url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal))
        || (Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)));

    private static bool IsFragmentCharacter(char character) =>
        char.IsLetterOrDigit(character) || character is '-' or '_' or '.';
}

public interface ISpeedReadingCmsMediaStorage
{
    Task<CmsStoredMedia> SaveAsync(
        Guid mediaId,
        CmsMediaUpload upload,
        string extension,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}

public static class CmsMediaPolicy
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public static string GetValidatedExtension(string contentType, string fileName, long sizeBytes)
    {
        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "The image must not be empty.");
        }

        if (sizeBytes > MaxFileSizeBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "The image exceeds the maximum size.");
        }

        if (string.IsNullOrWhiteSpace(fileName)
            || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("The image file name is invalid.", nameof(fileName));
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
            throw new ArgumentException("Only supported image types with matching extensions are allowed.", nameof(contentType));
        }

        return extension;
    }

    public static bool HasValidSignature(string contentType, ReadOnlySpan<byte> header)
    {
        var normalizedType = contentType.Trim().ToLowerInvariant();
        return normalizedType switch
        {
            "image/png" => header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/jpeg" => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/gif" => header.Length >= 4 && header[..4].SequenceEqual("GIF8"u8),
            "image/webp" => header.Length >= 12
                && header[..4].SequenceEqual("RIFF"u8)
                && header[8..12].SequenceEqual("WEBP"u8),
            _ => false
        };
    }
}

public sealed record CmsContactReplyEmail(string Subject, string Body);

public static class CmsContactReplyEmailFormatter
{
    public static CmsContactReplyEmail Create(
        string contactName,
        string contactSubject,
        string originalMessage,
        string replyContent)
    {
        var safeName = System.Text.Encodings.Web.HtmlEncoder.Default.Encode(contactName);
        var safeSubject = System.Text.Encodings.Web.HtmlEncoder.Default.Encode(contactSubject);
        var safeOriginalMessage = System.Text.Encodings.Web.HtmlEncoder.Default.Encode(originalMessage);
        var safeReplyContent = System.Text.Encodings.Web.HtmlEncoder.Default.Encode(replyContent);

        var body = $"""
            <html>
              <body style="font-family: sans-serif; line-height: 1.6;">
                <div style="max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;">
                  <h2 style="color: #4f46e5;">Destek talebiniz yanıtlandı</h2>
                  <p>Merhaba <strong>{safeName}</strong>,</p>
                  <p><strong>{safeSubject}</strong> konusundaki destek talebiniz yanıtlandı.</p>
                  <div style="background-color: #f9fafb; padding: 15px; border-radius: 8px; margin: 20px 0;">
                    <p style="margin: 0; font-size: 0.9em; color: #6b7280;">Mesajınız:</p>
                    <p style="margin: 5px 0 0 0; white-space: pre-wrap;">{safeOriginalMessage}</p>
                  </div>
                  <div style="background-color: #eef2ff; padding: 20px; border-radius: 8px; border-left: 4px solid #4f46e5;">
                    <p style="margin: 0; font-size: 0.9em; color: #4f46e5; font-weight: bold;">Yanıt:</p>
                    <p style="margin: 10px 0 0 0; white-space: pre-wrap;">{safeReplyContent}</p>
                  </div>
                  <p style="margin-top: 30px; font-size: 0.8em; color: #9ca3af;">Bu e-posta Hızlı Okuma otomatik sistemi tarafından gönderilmiştir.</p>
                </div>
              </body>
            </html>
            """;

        return new CmsContactReplyEmail(
            $"RE: {contactSubject} - Hızlı Okuma destek talebi yanıtı",
            body);
    }
}

public interface ISpeedReadingEmailDelivery
{
    Task QueueAsync(
        Guid messageId,
        string consumerType,
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}

public interface ISpeedReadingCms
{
    Task<IReadOnlyList<CmsContentBlockSummary>> GetLandingContentAsync(
        string group,
        string? language,
        CancellationToken cancellationToken = default);

    Task<CmsPageSummary?> GetPublishedPageAsync(string slug, CancellationToken cancellationToken = default);

    Task<SpeedReadingPage<CmsBlogPostSummary>> GetPublishedBlogPostsAsync(
        int pageNumber,
        int pageSize,
        string? tag,
        CancellationToken cancellationToken = default);

    Task<CmsBlogPostSummary?> GetPublishedBlogPostAsync(string slug, CancellationToken cancellationToken = default);

    Task<Guid> SubmitContactMessageAsync(
        CmsContactMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> SubscribeAsync(
        CmsNewsletterSubscriptionRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> UnsubscribeAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CmsContentBlockSummary>> GetContentBlocksAsync(
        string? group,
        CancellationToken cancellationToken = default);

    Task UpsertLandingContentAsync(
        string group,
        IReadOnlyDictionary<string, string> blocks,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateContentBlockAsync(
        CmsContentBlockRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateContentBlockAsync(
        Guid id,
        CmsContentBlockRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteContentBlockAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);

    Task<SpeedReadingPage<CmsMediaAssetSummary>> GetMediaAssetsAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<CmsMediaAssetSummary> UploadMediaAsync(
        CmsMediaUpload upload,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<CmsMediaDownload?> GetMediaDownloadAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteMediaAsync(
        Guid id,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CmsNavigationItemSummary>> GetNavigationAsync(
        string menu,
        bool includeHidden,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateNavigationItemAsync(
        CmsNavigationItemRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateNavigationItemAsync(
        Guid id,
        CmsNavigationItemRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteNavigationItemAsync(
        Guid id,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<SpeedReadingPage<CmsPageSummary>> GetPagesAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<CmsPageSummary?> GetPageAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CreatePageAsync(CmsPageRequest request, Guid actorId, CancellationToken cancellationToken = default);

    Task<bool> UpdatePageAsync(Guid id, CmsPageRequest request, Guid actorId, CancellationToken cancellationToken = default);

    Task<bool> DeletePageAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);

    Task<SpeedReadingPage<CmsBlogPostSummary>> GetBlogPostsAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<CmsBlogPostSummary?> GetBlogPostAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CreateBlogPostAsync(CmsBlogPostRequest request, Guid actorId, CancellationToken cancellationToken = default);

    Task<bool> UpdateBlogPostAsync(Guid id, CmsBlogPostRequest request, Guid actorId, CancellationToken cancellationToken = default);

    Task<bool> DeleteBlogPostAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);

    Task<SpeedReadingPage<CmsNewsletterSubscriberSummary>> GetSubscribersAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteSubscriberAsync(Guid id, bool hardDelete, Guid actorId, CancellationToken cancellationToken = default);

    Task<SpeedReadingPage<CmsContactMessageSummary>> GetContactMessagesAsync(
        int pageNumber,
        int pageSize,
        bool? isRead,
        bool? isReplied,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadContactMessageCountAsync(CancellationToken cancellationToken = default);

    Task<bool> MarkContactMessageAsReadAsync(Guid id, bool isRead, Guid actorId, CancellationToken cancellationToken = default);

    Task<bool> ReplyToContactMessageAsync(CmsContactReplyRequest request, Guid actorId, CancellationToken cancellationToken = default);

    Task<bool> DeleteContactMessageAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);
}
