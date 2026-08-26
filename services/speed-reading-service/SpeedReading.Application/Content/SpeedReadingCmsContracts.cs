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
