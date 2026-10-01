using System.Text.Json;
using System.IO;
using Coaching.Application.Content;
using Coaching.Application.Queries;
using Coaching.Application.Subscriptions;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Management;

public sealed class CoachingCmsService(CoachingDbContext db, ICoachingCmsMediaStorage mediaStorage) : ICoachingCms
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> ManagedGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "HomeHero", "HomeAreaHeading", "HomePage", "HomeProcessHeading", "HomeHowItWorks",
        "HomePlansHeading", "HomeBlogHeading", "HomeClosingCta", "HomeFaqHeading",
        "HomeTestimonialsHeading", "BlogLanding", "HomeBranding", "HomeFaq", "HomeTestimonials"
    };
    private static readonly HashSet<string> SingletonGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "HomeHero", "HomeAreaHeading", "HomeProcessHeading", "HomePlansHeading", "HomeBlogHeading",
        "HomeClosingCta", "HomeFaqHeading", "HomeTestimonialsHeading", "BlogLanding", "HomeBranding"
    };

    public async Task<PagedResponse<CoachingCmsEntrySummary>> GetEntriesAsync(
        string kind,
        int pageNumber,
        int pageSize,
        string? search,
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        kind = NormalizeKind(kind);
        ValidatePaging(pageNumber, pageSize);
        var query = db.CoachingCmsEntries.AsNoTracking().Where(entry => entry.Kind == kind);
        if (!string.IsNullOrWhiteSpace(group))
        {
            var normalizedGroup = Required(group, "Grup adı zorunludur.", 80);
            query = query.Where(entry => entry.Group == normalizedGroup);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(entry => entry.Title.Contains(term)
                || entry.Slug.Contains(term)
                || (entry.Summary != null && entry.Summary.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var entries = await query.OrderBy(entry => entry.SortOrder)
            .ThenByDescending(entry => entry.UpdatedAt ?? entry.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResponse<CoachingCmsEntrySummary>(entries.Select(ToSummary).ToArray(), pageNumber, pageSize, total);
    }

    public async Task<CoachingCmsEntrySummary?> GetEntryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await db.CoachingCmsEntries.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entry is null ? null : ToSummary(entry);
    }

    public async Task<Guid?> CreateEntryAsync(
        CoachingCmsEntryRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAndValidate(request);
        if (await db.CoachingCmsEntries.AnyAsync(
            entry => entry.Kind == normalized.Kind && entry.Slug == normalized.Slug,
            cancellationToken))
        {
            return null;
        }

        if (normalized.Kind == "Block" && IsSingletonGroup(normalized.Group)
            && await db.CoachingCmsEntries.AnyAsync(
                entry => entry.Kind == "Block" && entry.Group == normalized.Group,
                cancellationToken))
        {
            return null;
        }

        var entry = new CoachingCmsEntry
        {
            Kind = normalized.Kind,
            Group = normalized.Group,
            Title = normalized.Title,
            Slug = normalized.Slug,
            Summary = normalized.Summary,
            Content = normalized.Content,
            SeoTitle = normalized.SeoTitle,
            SeoDescription = normalized.SeoDescription,
            Eyebrow = normalized.Eyebrow,
            LinkLabel = normalized.LinkLabel,
            LinkUrl = normalized.LinkUrl,
            SecondaryLinkLabel = normalized.SecondaryLinkLabel,
            SecondaryLinkUrl = normalized.SecondaryLinkUrl,
            ImageUrl = normalized.ImageUrl,
            Author = normalized.Author,
            PublishedAt = normalized.PublishedAt,
            CoverImageUrl = normalized.CoverImageUrl,
            TestimonialConsentConfirmed = normalized.TestimonialConsentConfirmed,
            TagsJson = JsonSerializer.Serialize(normalized.Tags, JsonOptions),
            IsPublished = normalized.IsPublished,
            ScheduledPublishAt = normalized.ScheduledPublishAt,
            SortOrder = normalized.SortOrder,
            CreatedBy = actorId
        };
        db.CoachingCmsEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task<bool> UpdateEntryAsync(
        Guid id,
        CoachingCmsEntryRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAndValidate(request);
        var entry = await db.CoachingCmsEntries.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        if (await db.CoachingCmsEntries.AnyAsync(
                item => item.Id != id && item.Kind == normalized.Kind && item.Slug == normalized.Slug,
                cancellationToken)
            || (normalized.Kind == "Block" && IsSingletonGroup(normalized.Group)
                && await db.CoachingCmsEntries.AnyAsync(
                    item => item.Id != id && item.Kind == "Block" && item.Group == normalized.Group,
                    cancellationToken)))
        {
            return false;
        }

        db.CoachingCmsRevisions.Add(CreateRevision(entry, actorId));
        Apply(entry, normalized, actorId);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteEntryAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        _ = actorId;
        var entry = await db.CoachingCmsEntries.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        var revisions = await db.CoachingCmsRevisions.Where(item => item.EntryId == id).ToListAsync(cancellationToken);
        db.CoachingCmsRevisions.RemoveRange(revisions);
        db.CoachingCmsEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<CoachingCmsEntrySummary>> GetPublishedBlocksAsync(
        string group,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entries = await db.CoachingCmsEntries.AsNoTracking()
            .Where(entry => entry.Kind == "Block" && entry.Group == group && entry.IsPublished)
            .OrderBy(entry => entry.SortOrder)
            .ToListAsync(cancellationToken);
        return entries.Where(entry => CoachingCmsPublicationRules.IsPubliclyAvailable(entry.IsPublished, entry.ScheduledPublishAt, now))
            .Select(ToSummary)
            .ToArray();
    }

    public async Task<CoachingCmsEntrySummary?> GetPublishedPageAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = CoachingManagementRules.NormalizeSlug(slug);
        var now = DateTime.UtcNow;
        var entry = await db.CoachingCmsEntries.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Kind == "Page" && item.Slug == normalizedSlug && item.IsPublished, cancellationToken);
        return entry is not null && CoachingCmsPublicationRules.IsPubliclyAvailable(entry.IsPublished, entry.ScheduledPublishAt, now)
            ? ToSummary(entry)
            : null;
    }

    public async Task<PagedResponse<CoachingCmsEntrySummary>> GetPublishedBlogAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePaging(pageNumber, pageSize);
        var now = DateTime.UtcNow;
        var query = db.CoachingCmsEntries.AsNoTracking()
            .Where(entry => entry.Kind == "Blog" && entry.IsPublished
                && (!entry.ScheduledPublishAt.HasValue || entry.ScheduledPublishAt <= now));
        var total = await query.CountAsync(cancellationToken);
        var entries = await query.OrderByDescending(entry => entry.PublishedAt ?? entry.ScheduledPublishAt ?? entry.UpdatedAt ?? entry.CreatedAt)
            .ThenBy(entry => entry.SortOrder)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResponse<CoachingCmsEntrySummary>(entries.Select(ToSummary).ToArray(), pageNumber, pageSize, total);
    }

    public async Task<CoachingCmsEntrySummary?> GetPublishedBlogPostAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = CoachingManagementRules.NormalizeSlug(slug);
        var now = DateTime.UtcNow;
        var entry = await db.CoachingCmsEntries.SingleOrDefaultAsync(
            item => item.Kind == "Blog" && item.Slug == normalizedSlug && item.IsPublished
                && (!item.ScheduledPublishAt.HasValue || item.ScheduledPublishAt <= now),
            cancellationToken);
        if (entry is null)
        {
            return null;
        }

        entry.ViewCount++;
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(entry);
    }

    public async Task<IReadOnlyList<CoachingCmsRevisionSummary>> GetRevisionsAsync(
        Guid entryId,
        CancellationToken cancellationToken = default) =>
        await db.CoachingCmsRevisions.AsNoTracking()
            .Where(revision => revision.EntryId == entryId)
            .OrderByDescending(revision => revision.Version)
            .Select(revision => new CoachingCmsRevisionSummary(
                revision.Id, revision.EntryId, revision.Kind, revision.Version, revision.CreatedAt, revision.CreatedBy))
            .ToArrayAsync(cancellationToken);

    public async Task<bool> RestoreRevisionAsync(
        Guid entryId,
        Guid revisionId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var entry = await db.CoachingCmsEntries.SingleOrDefaultAsync(item => item.Id == entryId, cancellationToken);
        var revision = await db.CoachingCmsRevisions.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == revisionId && item.EntryId == entryId,
            cancellationToken);
        var previous = revision is null
            ? null
            : JsonSerializer.Deserialize<CoachingCmsEntrySummary>(revision.PayloadJson, JsonOptions);
        if (entry is null || previous is null)
        {
            return false;
        }

        db.CoachingCmsRevisions.Add(CreateRevision(entry, actorId));
        entry.Kind = previous.Kind;
        entry.Group = previous.Group;
        entry.Title = previous.Title;
        entry.Slug = previous.Slug;
        entry.Summary = previous.Summary;
        entry.Content = previous.Content;
        entry.SeoTitle = previous.SeoTitle;
        entry.SeoDescription = previous.SeoDescription;
        entry.Eyebrow = previous.Eyebrow;
        entry.LinkLabel = previous.LinkLabel;
        entry.LinkUrl = previous.LinkUrl;
        entry.SecondaryLinkLabel = previous.SecondaryLinkLabel;
        entry.SecondaryLinkUrl = previous.SecondaryLinkUrl;
        entry.ImageUrl = previous.ImageUrl;
        entry.Author = previous.Author;
        entry.PublishedAt = previous.PublishedAt;
        entry.CoverImageUrl = previous.CoverImageUrl;
        entry.TestimonialConsentConfirmed = previous.TestimonialConsentConfirmed;
        entry.TagsJson = JsonSerializer.Serialize(previous.Tags, JsonOptions);
        entry.IsPublished = previous.IsPublished;
        entry.ScheduledPublishAt = previous.ScheduledPublishAt;
        entry.SortOrder = previous.SortOrder;
        entry.UpdatedBy = actorId;
        entry.UpdatedAt = DateTime.UtcNow;
        entry.Version++;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<CoachingCmsNavigationItemSummary>> GetNavigationAsync(
        string menu,
        bool includeHidden,
        CancellationToken cancellationToken = default)
    {
        var query = db.CoachingCmsNavigationItems.AsNoTracking().Where(item => item.Menu == menu);
        if (!includeHidden)
        {
            query = query.Where(item => item.IsVisible);
        }

        return await query.OrderBy(item => item.SortOrder)
            .Select(item => new CoachingCmsNavigationItemSummary(
                item.Id, item.Menu, item.Label, item.Url, item.Icon, item.SortOrder, item.IsVisible, item.OpenInNewTab))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Guid?> CreateNavigationItemAsync(
        CoachingCmsNavigationItemRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeNavigation(request);
        var item = new CoachingCmsNavigationItem
        {
            Menu = normalized.Menu,
            Label = normalized.Label,
            Url = normalized.Url,
            Icon = normalized.Icon,
            SortOrder = normalized.SortOrder,
            IsVisible = normalized.IsVisible,
            OpenInNewTab = normalized.OpenInNewTab,
            CreatedBy = actorId
        };
        db.CoachingCmsNavigationItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    public async Task<bool> UpdateNavigationItemAsync(
        Guid id,
        CoachingCmsNavigationItemRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeNavigation(request);
        var item = await db.CoachingCmsNavigationItems.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (item is null)
        {
            return false;
        }

        item.Menu = normalized.Menu;
        item.Label = normalized.Label;
        item.Url = normalized.Url;
        item.Icon = normalized.Icon;
        item.SortOrder = normalized.SortOrder;
        item.IsVisible = normalized.IsVisible;
        item.OpenInNewTab = normalized.OpenInNewTab;
        item.UpdatedBy = actorId;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteNavigationItemAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        _ = actorId;
        var item = await db.CoachingCmsNavigationItems.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (item is null)
        {
            return false;
        }

        db.CoachingCmsNavigationItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PagedResponse<CoachingCmsMediaAssetSummary>> GetMediaAssetsAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePaging(pageNumber, pageSize);
        var query = db.CoachingCmsMediaAssets.AsNoTracking().Where(asset => !asset.IsDeleted);
        var total = await query.CountAsync(cancellationToken);
        var assets = await query.OrderByDescending(asset => asset.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResponse<CoachingCmsMediaAssetSummary>(assets.Select(ToMediaSummary).ToArray(), pageNumber, pageSize, total);
    }

    public async Task<CoachingCmsMediaAssetSummary> UploadMediaAsync(
        CoachingCmsMediaUpload upload,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty || upload.Content is null)
        {
            throw new ArgumentException("Görsel yükleme bilgileri geçersiz.");
        }

        var extension = CoachingCmsMediaPolicy.GetValidatedExtension(upload.ContentType, upload.FileName, upload.SizeBytes);
        var id = Guid.NewGuid();
        var stored = await mediaStorage.SaveAsync(id, upload, extension, cancellationToken);
        try
        {
            var asset = new CoachingCmsMediaAsset
            {
                Id = id,
                FileName = Path.GetFileName(upload.FileName),
                ContentType = upload.ContentType.Trim().ToLowerInvariant(),
                SizeBytes = stored.SizeBytes,
                Sha256 = stored.Sha256,
                StorageKey = stored.StorageKey,
                AltText = NormalizeOptional(upload.AltText, 300),
                CreatedBy = actorId
            };
            db.CoachingCmsMediaAssets.Add(asset);
            await db.SaveChangesAsync(cancellationToken);
            return ToMediaSummary(asset);
        }
        catch
        {
            await mediaStorage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<CoachingCmsMediaDownload?> GetMediaDownloadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var asset = await db.CoachingCmsMediaAssets.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (asset is null)
        {
            return null;
        }

        var content = await mediaStorage.OpenReadAsync(asset.StorageKey, cancellationToken);
        return content is null ? null : new CoachingCmsMediaDownload(content, asset.ContentType, asset.FileName);
    }

    public async Task<bool> DeleteMediaAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        var asset = await db.CoachingCmsMediaAssets.SingleOrDefaultAsync(
            item => item.Id == id && !item.IsDeleted,
            cancellationToken);
        if (asset is null)
        {
            return false;
        }

        asset.IsDeleted = true;
        asset.DeletedAt = DateTime.UtcNow;
        asset.DeletedBy = actorId;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        await mediaStorage.DeleteAsync(asset.StorageKey, cancellationToken);
        return true;
    }

    private static CoachingCmsEntryRequest NormalizeAndValidate(CoachingCmsEntryRequest request)
    {
        var kind = NormalizeKind(request.Kind);
        var title = Required(request.Title, "Başlık zorunludur.", 200);
        var slug = CoachingManagementRules.NormalizeSlug(string.IsNullOrWhiteSpace(request.Slug) ? title : request.Slug);
        var content = request.Content?.Trim() ?? string.Empty;
        var group = NormalizeGroup(request.Group);
        if (slug.Length is < 1 or > 160 || content.Length > 200_000 || (kind == "Block" && string.IsNullOrWhiteSpace(group)))
        {
            throw new ArgumentException("İçerik türü, adresi, grubu veya gövdesi geçersiz.");
        }

        if (group?.Length > 80 || request.SortOrder is < 0 or > 10_000)
        {
            throw new ArgumentException("Grup veya sıralama değeri sınırların dışında.");
        }
        if (kind == "Block" && group == "HomeTestimonials" && request.IsPublished && !request.TestimonialConsentConfirmed)
        {
            throw new ArgumentException("Kullanıcı yorumu yayınlanmadan önce açık yayın izni onaylanmalıdır.");
        }

        var tags = (request.Tags ?? Array.Empty<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(30)
            .ToArray();
        if (tags.Any(tag => tag.Length > 80))
        {
            throw new ArgumentException("Etiketler en fazla 80 karakter olabilir.");
        }

        var linkLabel = NormalizeOptional(request.LinkLabel, 150);
        var linkUrl = NormalizeSafeLink(request.LinkUrl);
        var secondaryLinkLabel = NormalizeOptional(request.SecondaryLinkLabel, 150);
        var secondaryLinkUrl = NormalizeSafeLink(request.SecondaryLinkUrl);
        if ((linkLabel is null) != (linkUrl is null) || (secondaryLinkLabel is null) != (secondaryLinkUrl is null))
        {
            throw new ArgumentException("Bağlantı etiketi ve adresi birlikte girilmelidir.");
        }

        return request with
        {
            Kind = kind,
            Group = group,
            Title = title,
            Slug = slug,
            Summary = NormalizeOptional(request.Summary, 500),
            Content = content,
            SeoTitle = NormalizeOptional(request.SeoTitle, 200),
            SeoDescription = NormalizeOptional(request.SeoDescription, 500),
            Eyebrow = NormalizeOptional(request.Eyebrow, 120),
            LinkLabel = linkLabel,
            LinkUrl = linkUrl,
            SecondaryLinkLabel = secondaryLinkLabel,
            SecondaryLinkUrl = secondaryLinkUrl,
            ImageUrl = NormalizeMediaUrl(request.ImageUrl),
            Author = NormalizeOptional(request.Author, 150),
            PublishedAt = request.PublishedAt?.ToUniversalTime(),
            CoverImageUrl = NormalizeMediaUrl(request.CoverImageUrl),
            TestimonialConsentConfirmed = request.TestimonialConsentConfirmed,
            Tags = tags,
            ScheduledPublishAt = request.ScheduledPublishAt?.ToUniversalTime()
        };
    }

    private static (string Menu, string Label, string Url, string? Icon, int SortOrder, bool IsVisible, bool OpenInNewTab)
        NormalizeNavigation(CoachingCmsNavigationItemRequest request)
    {
        var requestedMenu = Required(request.Menu, "Menü adı zorunludur.", 80);
        var menu = requestedMenu.ToLowerInvariant() switch
        {
            "main" => "Main",
            "footer" => "Footer",
            _ => throw new ArgumentException("Koçluk sitesi yalnızca Main ve Footer menülerini destekler.")
        };
        var label = Required(request.Label, "Menü etiketi zorunludur.", 150);
        var url = request.Url?.Trim() ?? string.Empty;
        if (!CoachingManagementRules.IsSafeNavigationUrl(url) || request.SortOrder is < 0 or > 10_000)
        {
            throw new ArgumentException("Menü bağlantısı veya sıralama değeri geçersiz.");
        }

        return (menu, label, url, NormalizeOptional(request.Icon, 80), request.SortOrder, request.IsVisible, request.OpenInNewTab);
    }

    private static string NormalizeKind(string? kind) => kind?.Trim().ToLowerInvariant() switch
    {
        "page" or "pages" => "Page",
        "blog" or "blogpost" or "blogposts" => "Blog",
        "block" or "blocks" => "Block",
        _ => throw new ArgumentException("İçerik türü Page, Blog veya Block olmalıdır.")
    };

    private static string Required(string? value, string message, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength)
        {
            throw new ArgumentException(message);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Bu alan en fazla {maxLength} karakter olabilir.");
        }

        return normalized;
    }

    private static void ValidatePaging(int pageNumber, int pageSize)
    {
        if (pageNumber is < 1 or > 1_000 || pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Sayfalama aralık dışı.");
        }
    }

    private static void Apply(CoachingCmsEntry entry, CoachingCmsEntryRequest request, Guid actorId)
    {
        entry.Kind = request.Kind;
        entry.Group = request.Group;
        entry.Title = request.Title;
        entry.Slug = request.Slug;
        entry.Summary = request.Summary;
        entry.Content = request.Content;
        entry.SeoTitle = request.SeoTitle;
        entry.SeoDescription = request.SeoDescription;
        entry.Eyebrow = request.Eyebrow;
        entry.LinkLabel = request.LinkLabel;
        entry.LinkUrl = request.LinkUrl;
        entry.SecondaryLinkLabel = request.SecondaryLinkLabel;
        entry.SecondaryLinkUrl = request.SecondaryLinkUrl;
        entry.ImageUrl = request.ImageUrl;
        entry.Author = request.Author;
        entry.PublishedAt = request.PublishedAt;
        entry.CoverImageUrl = request.CoverImageUrl;
        entry.TestimonialConsentConfirmed = request.TestimonialConsentConfirmed;
        entry.TagsJson = JsonSerializer.Serialize(request.Tags ?? Array.Empty<string>(), JsonOptions);
        entry.IsPublished = request.IsPublished;
        entry.ScheduledPublishAt = request.ScheduledPublishAt;
        entry.SortOrder = request.SortOrder;
        entry.UpdatedBy = actorId;
        entry.UpdatedAt = DateTime.UtcNow;
        entry.Version++;
    }

    private static CoachingCmsRevision CreateRevision(CoachingCmsEntry entry, Guid actorId) => new()
    {
        EntryId = entry.Id,
        Kind = entry.Kind,
        Version = entry.Version,
        PayloadJson = JsonSerializer.Serialize(ToSummary(entry), JsonOptions),
        CreatedBy = actorId
    };

    private static CoachingCmsEntrySummary ToSummary(CoachingCmsEntry entry) => new(
        entry.Id,
        entry.Kind,
        entry.Group,
        entry.Title,
        entry.Slug,
        entry.Summary,
        entry.Content,
        entry.SeoTitle,
        entry.SeoDescription,
        JsonSerializer.Deserialize<string[]>(entry.TagsJson, JsonOptions) ?? Array.Empty<string>(),
        entry.IsPublished,
        entry.ScheduledPublishAt,
        entry.SortOrder,
        entry.ViewCount,
        entry.CreatedAt,
        entry.UpdatedAt,
        entry.Version,
        entry.Eyebrow,
        entry.LinkLabel,
        entry.LinkUrl,
        entry.SecondaryLinkLabel,
        entry.SecondaryLinkUrl,
        entry.ImageUrl,
        entry.Author,
        entry.PublishedAt,
        entry.CoverImageUrl,
        entry.TestimonialConsentConfirmed);

    private static string? NormalizeSafeLink(string? value)
    {
        var normalized = NormalizeOptional(value, 500);
        if (normalized is not null && !CoachingManagementRules.IsSafeNavigationUrl(normalized))
        {
            throw new ArgumentException("Bağlantı yalnızca Koçluk içi bir yol veya HTTP(S) adresi olabilir.");
        }

        return normalized;
    }

    private static string? NormalizeGroup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var group = value.Trim();
        return ManagedGroups.FirstOrDefault(candidate => string.Equals(candidate, group, StringComparison.OrdinalIgnoreCase)) ?? group;
    }

    private static bool IsSingletonGroup(string? group) => group is not null && SingletonGroups.Contains(group);

    private static string? NormalizeMediaUrl(string? value)
    {
        var normalized = NormalizeOptional(value, 500);
        if (normalized is null)
        {
            return null;
        }

        const string prefix = "/api/coaching/cms/media/";
        return normalized.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParseExact(normalized[prefix.Length..], "D", out _)
                ? normalized
                : throw new ArgumentException("Görsel, Koçluk CMS medya kütüphanesinden seçilmelidir.");
    }

    private static CoachingCmsMediaAssetSummary ToMediaSummary(CoachingCmsMediaAsset asset) => new(
        asset.Id,
        asset.FileName,
        asset.ContentType,
        asset.SizeBytes,
        asset.Sha256,
        $"/api/coaching/cms/media/{asset.Id:D}",
        asset.AltText,
        asset.CreatedAt,
        asset.UpdatedAt);
}
