using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SpeedReading.Application.Content;

namespace SpeedReading.Infrastructure.Legacy;

public sealed class LegacySpeedReadingCms(
    SpeedReadingDbContext db,
    IMemoryCache cache) : ISpeedReadingCms
{
    private static readonly TimeSpan LandingCacheDuration = TimeSpan.FromMinutes(10);

    public async Task<IReadOnlyList<CmsContentBlockSummary>> GetLandingContentAsync(
        string group,
        string? language,
        CancellationToken cancellationToken = default)
    {
        var normalizedGroup = string.IsNullOrWhiteSpace(group) ? "HomePage" : group.Trim();
        var cacheKey = $"cms:landing:{normalizedGroup}:{language?.Trim() ?? ""}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<CmsContentBlockSummary>? cached) && cached is not null)
        {
            return cached;
        }

        var blockRows = await db.ContentBlocks
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.Group == normalizedGroup)
            .OrderBy(item => item.Key)
            .ToListAsync(cancellationToken);
        var blocks = blockRows.Select(ToSummary).ToList();

        cache.Set(cacheKey, blocks, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = LandingCacheDuration,
            Size = 1
        });
        return blocks;
    }

    public async Task<CmsPageSummary?> GetPublishedPageAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var page = await db.Pages
            .AsNoTracking()
            .SingleOrDefaultAsync(item => !item.IsDeleted && item.IsPublished && item.Slug == slug, cancellationToken);
        return page is null ? null : ToSummary(page);
    }

    public async Task<SpeedReadingPage<CmsBlogPostSummary>> GetPublishedBlogPostsAsync(
        int pageNumber,
        int pageSize,
        string? tag,
        CancellationToken cancellationToken = default)
    {
        var (page, size) = NormalizePage(pageNumber, pageSize);
        var query = db.BlogPosts
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.IsPublished);

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var normalizedTag = tag.Trim();
            query = query.Where(item => item.Tags != null && item.Tags.Contains(normalizedTag));
        }

        var total = await query.CountAsync(cancellationToken);
        var postRows = await query
            .OrderByDescending(item => item.PublishedAt)
            .ThenByDescending(item => item.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
        var posts = postRows.Select(ToSummary).ToList();

        return new SpeedReadingPage<CmsBlogPostSummary>(posts, page, size, total);
    }

    public async Task<CmsBlogPostSummary?> GetPublishedBlogPostAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var post = await db.BlogPosts
            .SingleOrDefaultAsync(item => !item.IsDeleted && item.IsPublished && item.Slug == slug, cancellationToken);
        if (post is null)
        {
            return null;
        }

        post.ViewCount++;
        post.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(post);
    }

    public async Task<Guid> SubmitContactMessageAsync(
        CmsContactMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var message = new LegacyContactMessage
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Subject = request.Subject.Trim(),
            Message = request.Message.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        };

        db.ContactMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        return message.Id;
    }

    public async Task<bool> SubscribeAsync(
        CmsNewsletterSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();
        var existing = await db.NewsletterSubscribers
            .SingleOrDefaultAsync(item => item.Email == email, cancellationToken);

        if (existing is not null)
        {
            if (!existing.IsActive || existing.IsDeleted)
            {
                existing.IsActive = true;
                existing.IsDeleted = false;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = Guid.Empty;
                await db.SaveChangesAsync(cancellationToken);
            }

            return false;
        }

        db.NewsletterSubscribers.Add(new LegacyNewsletterSubscriber
        {
            Id = Guid.NewGuid(),
            Email = email,
            IsActive = true,
            Source = "website",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.Empty
        });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnsubscribeAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (!SpeedReadingNewsletterRules.TryGetSubscriberId(token, out var subscriberId))
        {
            return false;
        }

        var subscriber = await db.NewsletterSubscribers
            .SingleOrDefaultAsync(item => item.Id == subscriberId && !item.IsDeleted, cancellationToken);
        if (subscriber is null)
        {
            return false;
        }

        if (!subscriber.IsActive)
        {
            return true;
        }

        subscriber.IsActive = false;
        subscriber.UpdatedAt = DateTime.UtcNow;
        subscriber.UpdatedBy = Guid.Empty;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<CmsContentBlockSummary>> GetContentBlocksAsync(
        string? group,
        CancellationToken cancellationToken = default)
    {
        var query = db.ContentBlocks.AsNoTracking().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(group))
        {
            query = query.Where(item => item.Group == group.Trim());
        }

        var rows = await query
            .OrderBy(item => item.Group)
            .ThenBy(item => item.Key)
            .ToListAsync(cancellationToken);
        return rows.Select(ToSummary).ToList();
    }

    public async Task UpsertLandingContentAsync(
        string group,
        IReadOnlyDictionary<string, string> blocks,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var normalizedGroup = group.Trim();
        var keys = blocks.Keys.ToArray();
        var existing = await db.ContentBlocks
            .Where(item => !item.IsDeleted && item.Group == normalizedGroup && keys.Contains(item.Key))
            .ToListAsync(cancellationToken);

        foreach (var (key, value) in blocks)
        {
            var block = existing.SingleOrDefault(item => item.Key == key);
            if (block is null)
            {
                db.ContentBlocks.Add(new LegacyContentBlock
                {
                    Id = Guid.NewGuid(),
                    Group = normalizedGroup,
                    Key = key,
                    Value = value,
                    Type = 1,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = actorId
                });
            }
            else
            {
                block.Value = value;
                block.UpdatedAt = DateTime.UtcNow;
                block.UpdatedBy = actorId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        RemoveLandingCache(normalizedGroup);
    }

    public async Task<Guid> CreateContentBlockAsync(
        CmsContentBlockRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var block = new LegacyContentBlock
        {
            Id = Guid.NewGuid(),
            Key = request.Key.Trim(),
            Group = request.Group.Trim(),
            Label = request.Label?.Trim(),
            Type = request.Type,
            Value = request.Value,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorId
        };
        db.ContentBlocks.Add(block);
        await db.SaveChangesAsync(cancellationToken);
        RemoveLandingCache(block.Group);
        return block.Id;
    }

    public async Task<bool> UpdateContentBlockAsync(
        Guid id,
        CmsContentBlockRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var block = await FindActiveAsync(db.ContentBlocks, id, cancellationToken);
        if (block is null)
        {
            return false;
        }

        var oldGroup = block.Group;
        block.Key = request.Key.Trim();
        block.Group = request.Group.Trim();
        block.Label = request.Label?.Trim();
        block.Type = request.Type;
        block.Value = request.Value;
        block.UpdatedAt = DateTime.UtcNow;
        block.UpdatedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        RemoveLandingCache(oldGroup);
        RemoveLandingCache(block.Group);
        return true;
    }

    public async Task<bool> DeleteContentBlockAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        var block = await FindActiveAsync(db.ContentBlocks, id, cancellationToken);
        if (block is null)
        {
            return false;
        }

        block.IsDeleted = true;
        block.DeletedAt = DateTime.UtcNow;
        block.DeletedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        RemoveLandingCache(block.Group);
        return true;
    }

    public Task<SpeedReadingPage<CmsPageSummary>> GetPagesAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
        GetPagePageAsync(pageNumber, pageSize, cancellationToken);

    public async Task<CmsPageSummary?> GetPageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var page = await FindActiveAsync(db.Pages, id, cancellationToken);
        return page is null ? null : ToSummary(page);
    }

    public async Task<Guid> CreatePageAsync(CmsPageRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var page = new LegacyPage
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorId
        };
        Apply(page, request);
        db.Pages.Add(page);
        await db.SaveChangesAsync(cancellationToken);
        return page.Id;
    }

    public async Task<bool> UpdatePageAsync(Guid id, CmsPageRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var page = await FindActiveAsync(db.Pages, id, cancellationToken);
        if (page is null)
        {
            return false;
        }

        Apply(page, request);
        page.UpdatedAt = DateTime.UtcNow;
        page.UpdatedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeletePageAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        var page = await FindActiveAsync(db.Pages, id, cancellationToken);
        if (page is null)
        {
            return false;
        }

        page.IsDeleted = true;
        page.DeletedAt = DateTime.UtcNow;
        page.DeletedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<SpeedReadingPage<CmsBlogPostSummary>> GetBlogPostsAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
        GetBlogPageAsync(pageNumber, pageSize, cancellationToken);

    public async Task<CmsBlogPostSummary?> GetBlogPostAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var post = await FindActiveAsync(db.BlogPosts, id, cancellationToken);
        return post is null ? null : ToSummary(post);
    }

    public async Task<Guid> CreateBlogPostAsync(CmsBlogPostRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var post = new LegacyBlogPost
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorId
        };
        Apply(post, request);
        db.BlogPosts.Add(post);
        await db.SaveChangesAsync(cancellationToken);
        return post.Id;
    }

    public async Task<bool> UpdateBlogPostAsync(Guid id, CmsBlogPostRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var post = await FindActiveAsync(db.BlogPosts, id, cancellationToken);
        if (post is null)
        {
            return false;
        }

        if (request.IsPublished && !post.IsPublished && request.PublishedAt is null)
        {
            request = request with { PublishedAt = DateTime.UtcNow };
        }

        Apply(post, request);
        post.UpdatedAt = DateTime.UtcNow;
        post.UpdatedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteBlogPostAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        var post = await FindActiveAsync(db.BlogPosts, id, cancellationToken);
        if (post is null)
        {
            return false;
        }

        post.IsDeleted = true;
        post.DeletedAt = DateTime.UtcNow;
        post.DeletedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SpeedReadingPage<CmsNewsletterSubscriberSummary>> GetSubscribersAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (page, size) = NormalizePage(pageNumber, pageSize);
        var query = db.NewsletterSubscribers.AsNoTracking().Where(item => !item.IsDeleted && item.IsActive);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
        var items = rows.Select(ToSummary).ToList();
        return new SpeedReadingPage<CmsNewsletterSubscriberSummary>(items, page, size, total);
    }

    public async Task<bool> DeleteSubscriberAsync(Guid id, bool hardDelete, Guid actorId, CancellationToken cancellationToken = default)
    {
        var subscriber = await db.NewsletterSubscribers
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (subscriber is null)
        {
            return false;
        }

        if (hardDelete)
        {
            db.NewsletterSubscribers.Remove(subscriber);
        }
        else
        {
            subscriber.IsActive = false;
            subscriber.UpdatedAt = DateTime.UtcNow;
            subscriber.UpdatedBy = actorId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SpeedReadingPage<CmsContactMessageSummary>> GetContactMessagesAsync(
        int pageNumber,
        int pageSize,
        bool? isRead,
        bool? isReplied,
        CancellationToken cancellationToken = default)
    {
        var (page, size) = NormalizePage(pageNumber, pageSize);
        var query = db.ContactMessages.AsNoTracking().Where(item => !item.IsDeleted);
        if (isRead.HasValue)
        {
            query = query.Where(item => item.IsRead == isRead.Value);
        }
        if (isReplied.HasValue)
        {
            query = query.Where(item => item.IsReplied == isReplied.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
        var items = rows.Select(ToSummary).ToList();
        return new SpeedReadingPage<CmsContactMessageSummary>(items, page, size, total);
    }

    public Task<int> GetUnreadContactMessageCountAsync(CancellationToken cancellationToken = default) =>
        db.ContactMessages.CountAsync(item => !item.IsDeleted && !item.IsRead, cancellationToken);

    public async Task<bool> MarkContactMessageAsReadAsync(Guid id, bool isRead, Guid actorId, CancellationToken cancellationToken = default)
    {
        var message = await FindActiveAsync(db.ContactMessages, id, cancellationToken);
        if (message is null)
        {
            return false;
        }

        message.IsRead = isRead;
        message.UpdatedAt = DateTime.UtcNow;
        message.UpdatedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReplyToContactMessageAsync(CmsContactReplyRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var message = await FindActiveAsync(db.ContactMessages, request.MessageId, cancellationToken);
        if (message is null)
        {
            return false;
        }

        message.ReplyContent = request.ReplyContent.Trim();
        message.RepliedAt = DateTime.UtcNow;
        message.RepliedBy = actorId;
        message.IsReplied = true;
        message.IsRead = true;
        message.UpdatedAt = DateTime.UtcNow;
        message.UpdatedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteContactMessageAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        var message = await FindActiveAsync(db.ContactMessages, id, cancellationToken);
        if (message is null)
        {
            return false;
        }

        message.IsDeleted = true;
        message.DeletedAt = DateTime.UtcNow;
        message.DeletedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<SpeedReadingPage<CmsPageSummary>> GetPagePageAsync(int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var (page, size) = NormalizePage(pageNumber, pageSize);
        var query = db.Pages.AsNoTracking().Where(item => !item.IsDeleted);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
        var items = rows.Select(ToSummary).ToList();
        return new SpeedReadingPage<CmsPageSummary>(items, page, size, total);
    }

    private async Task<SpeedReadingPage<CmsBlogPostSummary>> GetBlogPageAsync(int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var (page, size) = NormalizePage(pageNumber, pageSize);
        var query = db.BlogPosts.AsNoTracking().Where(item => !item.IsDeleted);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
        var items = rows.Select(ToSummary).ToList();
        return new SpeedReadingPage<CmsBlogPostSummary>(items, page, size, total);
    }

    private static void Apply(LegacyPage page, CmsPageRequest request)
    {
        page.Title = request.Title.Trim();
        page.Slug = request.Slug.Trim();
        page.Content = request.Content;
        page.IsPublished = request.IsPublished;
        page.MetaTitle = request.SeoSettings.MetaTitle;
        page.MetaDescription = request.SeoSettings.MetaDescription;
        page.MetaKeywords = request.SeoSettings.MetaKeywords;
        page.CanonicalUrl = request.SeoSettings.CanonicalUrl;
        page.OgTitle = request.SeoSettings.OgTitle;
        page.OgDescription = request.SeoSettings.OgDescription;
        page.OgImage = request.SeoSettings.OgImage;
        page.SeoSettingsNoIndex = request.SeoSettings.NoIndex;
    }

    private static void Apply(LegacyBlogPost post, CmsBlogPostRequest request)
    {
        post.Title = request.Title.Trim();
        post.Slug = request.Slug.Trim();
        post.Summary = request.Summary;
        post.Content = request.Content;
        post.Author = request.Author;
        post.PublishedAt = request.IsPublished ? request.PublishedAt ?? DateTime.UtcNow : request.PublishedAt;
        post.Tags = request.Tags is null ? null : string.Join(',', request.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()));
        post.CoverImageUrl = request.CoverImageUrl;
        post.IsPublished = request.IsPublished;
        post.MetaTitle = request.SeoSettings.MetaTitle;
        post.MetaDescription = request.SeoSettings.MetaDescription;
        post.MetaKeywords = request.SeoSettings.MetaKeywords;
        post.CanonicalUrl = request.SeoSettings.CanonicalUrl;
        post.OgTitle = request.SeoSettings.OgTitle;
        post.OgDescription = request.SeoSettings.OgDescription;
        post.OgImage = request.SeoSettings.OgImage;
        post.SeoSettingsNoIndex = request.SeoSettings.NoIndex;
    }

    private static CmsContentBlockSummary ToSummary(LegacyContentBlock item) =>
        new(item.Id, item.Key, item.Group, item.Label, item.Type, item.Value);

    private static CmsPageSummary ToSummary(LegacyPage item) =>
        new(
            item.Id,
            item.Title,
            item.Slug,
            item.Content,
            item.IsPublished,
            new CmsSeoSettings(item.MetaTitle, item.MetaDescription, item.MetaKeywords, item.CanonicalUrl, item.OgTitle, item.OgDescription, item.OgImage, item.SeoSettingsNoIndex),
            item.CreatedAt,
            item.UpdatedAt);

    private static CmsBlogPostSummary ToSummary(LegacyBlogPost item) =>
        new(
            item.Id,
            item.Title,
            item.Slug,
            item.Summary,
            item.Content,
            item.Author,
            item.PublishedAt,
            SplitTags(item.Tags),
            item.CoverImageUrl,
            new CmsSeoSettings(item.MetaTitle, item.MetaDescription, item.MetaKeywords, item.CanonicalUrl, item.OgTitle, item.OgDescription, item.OgImage, item.SeoSettingsNoIndex),
            item.ViewCount,
            item.IsPublished,
            item.CreatedAt,
            item.UpdatedAt);

    private static CmsContactMessageSummary ToSummary(LegacyContactMessage item) =>
        new(item.Id, item.Name, item.Email, item.Subject, item.Message, item.IsRead, item.IsReplied, item.RepliedAt, item.ReplyContent, item.CreatedAt, item.UpdatedAt);

    private static CmsNewsletterSubscriberSummary ToSummary(LegacyNewsletterSubscriber item) =>
        new(item.Id, item.Email, item.IsActive, item.Source, item.CreatedAt, item.UpdatedAt);

    private static IReadOnlyList<string> SplitTags(string? tags) =>
        string.IsNullOrWhiteSpace(tags)
            ? Array.Empty<string>()
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void RemoveLandingCache(string group)
    {
        cache.Remove($"cms:landing:{group}:");
        cache.Remove($"cms:landing:{group}:tr");
        cache.Remove($"cms:landing:{group}:en");
    }

    private static (int Page, int Size) NormalizePage(int pageNumber, int pageSize) =>
        (Math.Max(pageNumber, 1), Math.Clamp(pageSize, 1, 100));

    private static Task<TEntity?> FindActiveAsync<TEntity>(
        DbSet<TEntity> set,
        Guid id,
        CancellationToken cancellationToken)
        where TEntity : LegacyBaseEntity =>
        set.SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
}
