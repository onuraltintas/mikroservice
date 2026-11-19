using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Site.Infrastructure;

public sealed record SiteEntryRequest(
    string Kind,
    string? Group,
    string? Slug,
    string Title,
    string? Subtitle,
    string? Body,
    IReadOnlyList<string>? Highlights,
    string? Brand,
    string? Icon,
    string? LinkLabel,
    string? LinkUrl,
    string? SecondaryLinkLabel,
    string? SecondaryLinkUrl,
    Guid? ImageId,
    string? CtaTitle,
    string? CtaBody,
    string? SeoTitle,
    string? SeoDescription,
    int SortOrder,
    bool IsPublished,
    DateTime? PublishAt,
    int? Version = null,
    string? Site = null,
    string? Category = null,
    string? Audience = null,
    DateTime? EndsAt = null,
    string? Author = null);

public sealed record SiteEntryView(
    Guid Id,
    string Kind,
    string Group,
    string? Slug,
    string Title,
    string? Subtitle,
    string Body,
    IReadOnlyList<string> Highlights,
    string? Brand,
    string? Icon,
    string? LinkLabel,
    string? LinkUrl,
    string? SecondaryLinkLabel,
    string? SecondaryLinkUrl,
    Guid? ImageId,
    string? ImageUrl,
    string? CtaTitle,
    string? CtaBody,
    string? SeoTitle,
    string? SeoDescription,
    int SortOrder,
    bool IsPublished,
    DateTime? PublishAt,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string Site,
    string? Category,
    string? Audience,
    DateTime? EndsAt,
    string? Author);

/// <summary>What a visitor sees of an entry: no publishing details.</summary>
public sealed record SitePublicEntry(
    Guid Id,
    string Kind,
    string? Slug,
    string Title,
    string? Subtitle,
    string Body,
    IReadOnlyList<string> Highlights,
    string? Brand,
    string? Icon,
    string? LinkLabel,
    string? LinkUrl,
    string? SecondaryLinkLabel,
    string? SecondaryLinkUrl,
    string? ImageUrl,
    string? CtaTitle,
    string? CtaBody,
    string? SeoTitle,
    string? SeoDescription,
    string? Category = null,
    string? Audience = null,
    string? Author = null,
    DateTime? PublishedAt = null);

public sealed record SiteHomeView(
    SitePublicEntry? Site,
    SitePublicEntry? Hero,
    IReadOnlyList<SitePublicEntry> Sections,
    IReadOnlyList<SitePublicEntry> Products,
    IReadOnlyList<SitePublicEntry> Audiences,
    IReadOnlyList<SitePublicEntry> Faqs);

public sealed record SitePageView(
    SitePublicEntry Page,
    SitePublicEntry? Site,
    IReadOnlyList<SitePublicEntry> Sections,
    IReadOnlyList<SitePublicEntry> Features,
    IReadOnlyList<SitePublicEntry> Audiences,
    IReadOnlyList<SitePublicEntry> Steps,
    IReadOnlyList<SitePublicEntry> Faqs);

public sealed record SiteSitemapEntry(string Slug, DateTime LastModified);

/// <summary>maxkampus.com's home page and the parts every page shares (settings, menus).</summary>
public sealed record MaxkampusHomeView(
    SitePublicEntry? Site,
    SitePublicEntry? Hero,
    IReadOnlyList<SitePublicEntry> Sections,
    IReadOnlyList<SitePublicEntry> Features,
    IReadOnlyList<SitePublicEntry> Steps,
    IReadOnlyList<SitePublicEntry> Reviews,
    IReadOnlyList<SitePublicEntry> Faqs,
    IReadOnlyList<SitePublicEntry> Menus,
    IReadOnlyList<SitePublicEntry> Plans);

public sealed record SitePostPage(int Total, IReadOnlyList<SitePublicEntry> Items, IReadOnlyList<string> Categories);
public sealed record SiteSitemapItem(string Path, DateTime LastModified);
public sealed record SiteRevisionView(Guid Id, int Version, DateTime CreatedAt, string Title);
public sealed record SiteOrderRequest(IReadOnlyList<Guid> Ids);
public sealed record SiteMediaView(Guid Id, string FileName, string ContentType, long SizeBytes, string? AltText, string Url, DateTime CreatedAt);
public sealed record SiteMediaFile(byte[] Content, string ContentType, string FileName, string Sha256);

/// <summary>Reading, editing and publishing the sites' content (eduivme.com and maxkampus.com).</summary>
public sealed class SiteContent(SiteDbContext db, TimeProvider clock)
{
    public const string MediaPath = "/api/site/media/";
    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);

    // ---- Visitors ----

    public async Task<SiteHomeView> GetHomeAsync(CancellationToken cancellationToken)
    {
        var entries = await Published().Where(item => item.Group == string.Empty).ToListAsync(cancellationToken);
        return new SiteHomeView(
            entries.Where(item => item.Kind == SiteKinds.Site).Select(Public).FirstOrDefault(),
            entries.Where(item => item.Kind == SiteKinds.Hero).Select(Public).FirstOrDefault(),
            Of(entries, SiteKinds.Section), Of(entries, SiteKinds.Product), Of(entries, SiteKinds.Audience), Of(entries, SiteKinds.Faq));
    }

    public async Task<SitePageView?> GetPageAsync(string slug, CancellationToken cancellationToken)
    {
        if (!SiteRules.SlugPattern().IsMatch(slug)) return null;
        var page = await Published().FirstOrDefaultAsync(item => item.Kind == SiteKinds.Page && item.Group == string.Empty && item.Slug == slug, cancellationToken);
        if (page is null) return null;
        var entries = await Published().Where(item => item.Group == slug).ToListAsync(cancellationToken);
        var site = await Published().FirstOrDefaultAsync(item => item.Kind == SiteKinds.Site && item.Group == string.Empty, cancellationToken);
        return new SitePageView(Public(page), site is null ? null : Public(site), Of(entries, SiteKinds.Section), Of(entries, SiteKinds.Feature),
            Of(entries, SiteKinds.Audience), Of(entries, SiteKinds.Step), Of(entries, SiteKinds.Faq));
    }

    public async Task<IReadOnlyList<SiteSitemapEntry>> GetSitemapAsync(CancellationToken cancellationToken) =>
        await Published().Where(item => item.Kind == SiteKinds.Page && item.Group == string.Empty && item.Slug != null)
            .OrderBy(item => item.SortOrder)
            .Select(item => new SiteSitemapEntry(item.Slug!, item.UpdatedAt))
            .ToListAsync(cancellationToken);

    // ---- maxkampus.com ----

    public async Task<MaxkampusHomeView> GetMaxkampusHomeAsync(CancellationToken cancellationToken)
    {
        var kinds = new[] { SiteKinds.Site, SiteKinds.Hero, SiteKinds.Section, SiteKinds.Feature, SiteKinds.Step, SiteKinds.Review,
            SiteKinds.Faq, SiteKinds.Menu, SiteKinds.Plan };
        var entries = await Published(SiteRules.Maxkampus).Where(item => kinds.Contains(item.Kind)).ToListAsync(cancellationToken);
        return new MaxkampusHomeView(
            entries.Where(item => item.Kind == SiteKinds.Site).Select(Public).FirstOrDefault(),
            entries.Where(item => item.Kind == SiteKinds.Hero).Select(Public).FirstOrDefault(),
            Of(entries, SiteKinds.Section), Of(entries, SiteKinds.Feature), Of(entries, SiteKinds.Step), Of(entries, SiteKinds.Review),
            Of(entries, SiteKinds.Faq), Of(entries, SiteKinds.Menu), Of(entries, SiteKinds.Plan));
    }

    /// <summary>A content page of maxkampus.com (/sayfa/{slug}).</summary>
    public async Task<SitePublicEntry?> GetMaxkampusPageAsync(string slug, CancellationToken cancellationToken) =>
        SiteRules.SlugPattern().IsMatch(slug)
            ? await Published(SiteRules.Maxkampus).Where(item => item.Kind == SiteKinds.Page && item.Slug == slug)
                .Select(item => item).FirstOrDefaultAsync(cancellationToken) is { } page ? Public(page) : null
            : null;

    public async Task<SitePostPage> GetPostsAsync(string? category, int skip, int take, CancellationToken cancellationToken)
    {
        var posts = Published(SiteRules.Maxkampus).Where(item => item.Kind == SiteKinds.Post);
        var categories = await posts.Where(item => item.Category != null).Select(item => item.Category!).Distinct().OrderBy(item => item)
            .ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(category)) posts = posts.Where(item => item.Category == category.Trim());
        var total = await posts.CountAsync(cancellationToken);
        var items = await posts.OrderByDescending(item => item.PublishAt ?? item.CreatedAt).ThenBy(item => item.Id)
            .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 50)).ToListAsync(cancellationToken);
        // A list shows the summary; the text comes with the post itself.
        return new SitePostPage(total, items.Select(item => Public(item) with { Body = string.Empty }).ToList(), categories);
    }

    public async Task<SitePublicEntry?> GetPostAsync(string slug, CancellationToken cancellationToken) =>
        SiteRules.SlugPattern().IsMatch(slug)
            && await Published(SiteRules.Maxkampus).FirstOrDefaultAsync(item => item.Kind == SiteKinds.Post && item.Slug == slug, cancellationToken) is { } post
            ? Public(post) : null;

    /// <summary>Panel notices for owners or teachers (and "all").</summary>
    public async Task<IReadOnlyList<SitePublicEntry>> GetNoticesAsync(string? audience, CancellationToken cancellationToken)
    {
        var wanted = audience is "owners" or "teachers" ? audience : "all";
        var notices = await Published(SiteRules.Maxkampus)
            .Where(item => item.Kind == SiteKinds.Notice && (item.Audience == "all" || item.Audience == wanted))
            .ToListAsync(cancellationToken);
        return Of(notices, SiteKinds.Notice);
    }

    public async Task<IReadOnlyList<SiteSitemapItem>> GetMaxkampusSitemapAsync(CancellationToken cancellationToken)
    {
        var entries = await Published(SiteRules.Maxkampus)
            .Where(item => (item.Kind == SiteKinds.Post || item.Kind == SiteKinds.Page) && item.Slug != null)
            .Select(item => new { item.Kind, item.Slug, item.UpdatedAt }).ToListAsync(cancellationToken);
        return entries.Select(item => new SiteSitemapItem((item.Kind == SiteKinds.Post ? "/blog/" : "/sayfa/") + item.Slug, item.UpdatedAt))
            .OrderBy(item => item.Path).ToList();
    }

    public async Task<SiteMediaFile?> GetMediaFileAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Media.AsNoTracking().Where(item => item.Id == id)
            .Select(item => new SiteMediaFile(item.Content, item.ContentType, item.FileName, item.Sha256))
            .FirstOrDefaultAsync(cancellationToken);

    // ---- Admin ----

    public async Task<IReadOnlyList<SiteEntryView>> GetEntriesAsync(string? group, CancellationToken cancellationToken, string? site = null)
    {
        var siteKey = string.IsNullOrWhiteSpace(site) ? SiteRules.Eduivme : site.Trim();
        if (!SiteRules.Sites.Contains(siteKey)) throw Invalid("site", "Site bilinmiyor.");
        var query = db.Entries.AsNoTracking().Where(item => item.Site == siteKey);
        if (group is not null) query = query.Where(item => item.Group == group);
        var entries = await query.OrderBy(item => item.Group).ThenBy(item => item.Kind).ThenBy(item => item.SortOrder).ThenBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return entries.Select(View).ToList();
    }

    public async Task<SiteEntryView?> GetEntryAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Entries.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken) is { } entry ? View(entry) : null;

    public async Task<SiteEntryView> CreateAsync(SiteEntryRequest request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var entry = new SiteEntry { Id = Guid.NewGuid(), CreatedAt = now, UpdatedAt = now, Version = 1 };
        await ApplyAsync(entry, request, cancellationToken);
        db.Entries.Add(entry);
        await SaveAsync(cancellationToken);
        return View(entry);
    }

    public async Task<SiteEntryView> UpdateAsync(Guid id, SiteEntryRequest request, CancellationToken cancellationToken)
    {
        var entry = await db.Entries.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw SiteRequestException.NotFound("İçerik bulunamadı.");
        if (request.Version is { } version && version != entry.Version)
            throw SiteRequestException.Conflict("Bu içerik siz düzenlerken değişti. Sayfayı yenileyip yeniden deneyin.");
        if (entry.Kind != request.Kind?.Trim())
            throw Invalid(nameof(request.Kind), "İçeriğin türü değiştirilemez.");
        if (entry.Site != (string.IsNullOrWhiteSpace(request.Site) ? SiteRules.Eduivme : request.Site.Trim()))
            throw Invalid("site", "İçeriğin sitesi değiştirilemez.");
        var oldSlug = entry.Slug;
        AddRevision(entry);
        await ApplyAsync(entry, request, cancellationToken);
        entry.Version++;
        entry.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        // A product page keeps its own entries when its address changes.
        if (entry.Kind == SiteKinds.Page && entry.Site == SiteRules.Eduivme && oldSlug is not null && entry.Slug != oldSlug)
        {
            var children = await db.Entries.Where(item => item.Site == SiteRules.Eduivme && item.Group == oldSlug).ToListAsync(cancellationToken);
            foreach (var child in children) child.Group = entry.Slug!;
        }
        await SaveAsync(cancellationToken);
        return View(entry);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = await db.Entries.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entry is null) return false;
        if (entry.Kind is SiteKinds.Site or SiteKinds.Hero)
            throw Invalid(nameof(entry.Kind), "Site ayarları ve giriş bölümü silinemez; yayından kaldırabilirsiniz.");
        var removed = new List<SiteEntry> { entry };
        if (entry.Kind == SiteKinds.Page && entry.Site == SiteRules.Eduivme && entry.Slug is not null)
            removed.AddRange(await db.Entries.Where(item => item.Site == SiteRules.Eduivme && item.Group == entry.Slug).ToListAsync(cancellationToken));
        var ids = removed.Select(item => item.Id).ToList();
        db.Revisions.RemoveRange(db.Revisions.Where(item => ids.Contains(item.EntryId)));
        db.Entries.RemoveRange(removed);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ReorderAsync(SiteOrderRequest request, CancellationToken cancellationToken)
    {
        var ids = request.Ids?.Distinct().ToList() ?? [];
        if (ids.Count == 0 || ids.Count > 200) throw Invalid("ids", "Sıralanacak içerikleri gönderin.");
        var entries = await db.Entries.Where(item => ids.Contains(item.Id)).ToListAsync(cancellationToken);
        if (entries.Count != ids.Count) throw SiteRequestException.NotFound("İçeriklerden biri bulunamadı.");
        if (entries.Select(item => (item.Site, item.Kind, item.Group)).Distinct().Count() != 1)
            throw Invalid("ids", "Yalnız aynı bölümdeki içerikler birlikte sıralanabilir.");
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var entry in entries)
        {
            var order = (ids.IndexOf(entry.Id) + 1) * 10;
            if (entry.SortOrder == order) continue;
            entry.SortOrder = order;
            entry.UpdatedAt = now;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SiteRevisionView>> GetRevisionsAsync(Guid id, CancellationToken cancellationToken)
    {
        var revisions = await db.Revisions.AsNoTracking().Where(item => item.EntryId == id)
            .OrderByDescending(item => item.Version).Take(50).ToListAsync(cancellationToken);
        return revisions.Select(item => new SiteRevisionView(item.Id, item.Version, item.CreatedAt,
            JsonSerializer.Deserialize<SiteEntrySnapshot>(item.Snapshot, SnapshotJson)?.Title ?? string.Empty)).ToList();
    }

    public async Task<SiteEntryView> RestoreAsync(Guid id, Guid revisionId, CancellationToken cancellationToken)
    {
        var entry = await db.Entries.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw SiteRequestException.NotFound("İçerik bulunamadı.");
        var revision = await db.Revisions.AsNoTracking().FirstOrDefaultAsync(item => item.Id == revisionId && item.EntryId == id, cancellationToken)
            ?? throw SiteRequestException.NotFound("Sürüm bulunamadı.");
        var snapshot = JsonSerializer.Deserialize<SiteEntrySnapshot>(revision.Snapshot, SnapshotJson)
            ?? throw SiteRequestException.NotFound("Sürüm okunamadı.");
        return await UpdateAsync(id, snapshot.ToRequest(entry.Kind, entry.Version, entry.Site), cancellationToken);
    }

    public async Task<IReadOnlyList<SiteMediaView>> GetMediaAsync(CancellationToken cancellationToken) =>
        (await db.Media.AsNoTracking().OrderByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, item.FileName, item.ContentType, item.SizeBytes, item.AltText, item.CreatedAt })
            .ToListAsync(cancellationToken))
        .Select(item => new SiteMediaView(item.Id, item.FileName, item.ContentType, item.SizeBytes, item.AltText, MediaPath + item.Id, item.CreatedAt))
        .ToList();

    public async Task<SiteMediaView> UploadMediaAsync(string fileName, byte[] content, string? altText, CancellationToken cancellationToken)
    {
        if (content.Length == 0) throw Invalid("file", "Bir görsel seçin.");
        if (content.Length > SiteRules.MaxMediaBytes) throw Invalid("file", "Görsel en çok 2 MB olabilir.");
        var contentType = DetectImage(content) ?? throw Invalid("file", "Yalnız PNG, JPEG ya da WebP görseller yüklenebilir.");
        var alt = Optional(altText, SiteRules.SubtitleLength, "altText", out var altError);
        if (altError is not null) throw altError;
        var safeName = new string(Path.GetFileName(fileName ?? string.Empty).Where(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_').ToArray());
        if (string.IsNullOrEmpty(safeName)) safeName = "gorsel";
        if (safeName.Length > 200) safeName = safeName[^200..];
        var media = new SiteMedia
        {
            Id = Guid.NewGuid(),
            FileName = safeName,
            ContentType = contentType,
            SizeBytes = content.Length,
            Content = content,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(content)),
            AltText = alt,
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };
        db.Media.Add(media);
        await db.SaveChangesAsync(cancellationToken);
        return new SiteMediaView(media.Id, media.FileName, media.ContentType, media.SizeBytes, media.AltText, MediaPath + media.Id, media.CreatedAt);
    }

    public async Task<bool> DeleteMediaAsync(Guid id, CancellationToken cancellationToken)
    {
        var media = await db.Media.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (media is null) return false;
        if (await db.Entries.AnyAsync(item => item.ImageId == id, cancellationToken))
            throw SiteRequestException.Conflict("Bu görsel bir içerikte kullanılıyor; önce içerikten kaldırın.");
        db.Media.Remove(media);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Rules ----

    private async Task ApplyAsync(SiteEntry entry, SiteEntryRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        void Error(string field, string message) => errors[field] = [message];

        var site = string.IsNullOrWhiteSpace(request.Site) ? SiteRules.Eduivme : request.Site.Trim();
        var maxkampus = site == SiteRules.Maxkampus;
        if (!SiteRules.Sites.Contains(site)) Error("site", "Site bilinmiyor.");
        var kind = request.Kind?.Trim() ?? string.Empty;
        var group = request.Group?.Trim() ?? string.Empty;
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? null : request.Slug.Trim().ToLowerInvariant();

        if (maxkampus && group.Length > 0) Error("group", "maxkampüs sitesinde içerik grubu yoktur.");
        if (group.Length > 0 && !SiteRules.SlugPattern().IsMatch(group)) Error("group", "Sayfa adresi geçersiz.");
        var kinds = maxkampus ? SiteKinds.MaxkampusKinds : group.Length == 0 ? SiteKinds.HomeKinds : SiteKinds.PageKinds;
        if (!kinds.Contains(kind)) Error("kind", maxkampus ? "Bu içerik türü maxkampüs sitesinde kullanılamaz."
            : group.Length == 0 ? "Bu içerik türü ana sayfada kullanılamaz." : "Bu içerik türü ürün sayfasında kullanılamaz.");
        if (!maxkampus && group.Length > 0 && !await db.Entries.AnyAsync(item => item.Site == SiteRules.Eduivme && item.Kind == SiteKinds.Page
                && item.Group == string.Empty && item.Slug == group, cancellationToken))
            Error("group", "Bu adreste bir ürün sayfası yok.");

        var category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        var audience = string.IsNullOrWhiteSpace(request.Audience) ? null : request.Audience.Trim();
        var author = string.IsNullOrWhiteSpace(request.Author) ? null : request.Author.Trim();
        if (category is { Length: > 60 }) Error("category", "Konu en çok 60 karakter olabilir.");
        if (author is { Length: > 120 }) Error("author", "Yazar adı en çok 120 karakter olabilir.");
        if (kind == SiteKinds.Menu && (category is null || !SiteKinds.MenuPlaces.Contains(category))) Error("category", "Menünün yeri üst menü (header) ya da alt bilgi (footer) olmalı.");
        if (kind == SiteKinds.Notice && (audience is null || !SiteKinds.NoticeAudiences.Contains(audience))) Error("audience", "Duyurunun kime gösterileceğini seçin.");
        if (kind != SiteKinds.Notice) audience = null;
        if (kind == SiteKinds.Plan && category is not null && category != "featured") Error("category", "Plan için yalnız \"en çok tercih edilen\" işareti seçilebilir.");
        if (kind is not (SiteKinds.Post or SiteKinds.Menu or SiteKinds.Plan)) category = null;
        if (kind != SiteKinds.Post) author = null;
        var endsAt = request.EndsAt is { } ends ? DateTime.SpecifyKind(ends.ToUniversalTime(), DateTimeKind.Utc) : (DateTime?)null;
        if (endsAt is not null && request.PublishAt is { } starts && endsAt <= starts.ToUniversalTime()) Error("endsAt", "Bitiş, başlangıçtan sonra olmalı.");

        switch (kind)
        {
            case SiteKinds.Site: slug = "site"; break;
            case SiteKinds.Hero: slug = "hero"; break;
            case SiteKinds.Plan:
                if (slug is null || !Guid.TryParse(slug, out _)) Error("slug", "Hangi plan için olduğunu seçin.");
                break;
            case SiteKinds.Post:
                if (slug is null || slug.Length > 80 || !SiteRules.SlugPattern().IsMatch(slug))
                    Error("slug", "Yazının adresi küçük harf, rakam ve tire içerebilir (örnek: veliyle-iletisim).");
                break;
            case SiteKinds.Page when maxkampus:
                if (slug is null || slug.Length > 80 || !SiteRules.SlugPattern().IsMatch(slug))
                    Error("slug", "Sayfa adresi küçük harf, rakam ve tire içerebilir (örnek: hakkimizda).");
                break;
            case SiteKinds.Section:
                var sections = maxkampus ? SiteKinds.MaxkampusSections : group.Length == 0 ? SiteKinds.HomeSections : SiteKinds.PageSections;
                if (slug is null || !sections.Contains(slug)) Error("slug", $"Bölüm şunlardan biri olmalı: {string.Join(", ", sections)}.");
                break;
            case SiteKinds.Page:
                if (slug is null || slug.Length > 80 || !SiteRules.SlugPattern().IsMatch(slug))
                    Error("slug", "Sayfa adresi küçük harf, rakam ve tire içerebilir (örnek: kurs-yonetimi).");
                break;
            default:
                if (slug is not null && (slug.Length > 80 || !SiteRules.SlugPattern().IsMatch(slug))) Error("slug", "Kısa ad küçük harf, rakam ve tire içerebilir.");
                break;
        }
        if (slug is not null && !errors.ContainsKey("slug") && await db.Entries.AnyAsync(item => item.Id != entry.Id && item.Site == site && item.Kind == kind
                && item.Group == group && item.Slug == slug, cancellationToken))
            Error("slug", kind is SiteKinds.Site or SiteKinds.Hero ? "Bu içerik zaten var; mevcut olanı düzenleyin." : "Bu kısa ad kullanılıyor.");

        var title = request.Title?.Trim() ?? string.Empty;
        if (title.Length == 0) Error("title", "Başlık gerekli.");
        else if (title.Length > SiteRules.TitleLength) Error("title", $"Başlık en çok {SiteRules.TitleLength} karakter olabilir.");
        var body = request.Body?.Trim() ?? string.Empty;
        var bodyLimit = kind is SiteKinds.Post or SiteKinds.Page && maxkampus ? SiteRules.LongBodyLength : SiteRules.BodyLength;
        if (body.Length > bodyLimit) Error("body", $"Metin en çok {bodyLimit} karakter olabilir.");
        if (kind is SiteKinds.Post or SiteKinds.Review or SiteKinds.Notice && body.Length == 0) Error("body", "Metin gerekli.");

        var highlights = (request.Highlights ?? []).Select(item => item?.Trim() ?? string.Empty).Where(item => item.Length > 0).ToList();
        if (highlights.Count > SiteRules.MaxHighlights) Error("highlights", $"En çok {SiteRules.MaxHighlights} madde eklenebilir.");
        if (highlights.Any(item => item.Length > SiteRules.HighlightLength)) Error("highlights", $"Bir madde en çok {SiteRules.HighlightLength} karakter olabilir.");

        var brand = string.IsNullOrWhiteSpace(request.Brand) ? null : request.Brand.Trim();
        if (brand is not null && !SiteRules.Brands.Contains(brand)) Error("brand", "Marka rengi geçersiz.");
        var icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim();
        if (icon is not null && !SiteRules.Icons.Contains(icon)) Error("icon", "Simge geçersiz.");

        string? Text(string? value, int max, string field)
        {
            var result = Optional(value, max, field, out var error);
            if (error is not null) errors[field] = error.Errors[field];
            return result;
        }
        string? Url(string? value, string field)
        {
            var result = Text(value, SiteRules.UrlLength, field);
            if (result is not null && !SiteRules.IsSafeUrl(result)) Error(field, "Adres https:// ile ya da / ile başlamalı.");
            return result;
        }

        var subtitle = Text(request.Subtitle, SiteRules.SubtitleLength, "subtitle");
        var linkLabel = Text(request.LinkLabel, SiteRules.LabelLength, "linkLabel");
        var linkUrl = Url(request.LinkUrl, "linkUrl");
        var secondaryLabel = Text(request.SecondaryLinkLabel, SiteRules.LabelLength, "secondaryLinkLabel");
        var secondaryUrl = Url(request.SecondaryLinkUrl, "secondaryLinkUrl");
        var ctaTitle = Text(request.CtaTitle, SiteRules.TitleLength, "ctaTitle");
        var ctaBody = Text(request.CtaBody, SiteRules.SubtitleLength, "ctaBody");
        var seoTitle = Text(request.SeoTitle, SiteRules.SeoTitleLength, "seoTitle");
        var seoDescription = Text(request.SeoDescription, SiteRules.SubtitleLength, "seoDescription");
        if (linkLabel is not null && linkUrl is null) Error("linkUrl", "Düğme yazısı varsa adres de gerekli.");
        if (secondaryLabel is not null && secondaryUrl is null) Error("secondaryLinkUrl", "İkinci düğmenin yazısı varsa adresi de gerekli.");
        if (kind == SiteKinds.Product && linkUrl is null) Error("linkUrl", "Karta tıklanınca gidilecek adres gerekli.");
        if (kind == SiteKinds.Menu && linkUrl is null) Error("linkUrl", "Menü bağlantısının adresi gerekli.");

        if (request.ImageId is { } imageId && !await db.Media.AnyAsync(item => item.Id == imageId, cancellationToken))
            Error("imageId", "Görsel bulunamadı.");
        if (request.SortOrder is < 0 or > 100000) Error("sortOrder", "Sıra 0 ile 100000 arasında olmalı.");

        if (errors.Count > 0) throw new SiteRequestException(400, "İçerik kaydedilemedi; işaretli alanları düzeltin.", errors);

        entry.Site = site;
        entry.Kind = kind;
        entry.Group = group;
        entry.Category = category;
        entry.Audience = audience;
        entry.Author = author;
        entry.EndsAt = endsAt;
        entry.Slug = slug;
        entry.Title = title;
        entry.Subtitle = subtitle;
        entry.Body = body;
        entry.Highlights = highlights;
        entry.Brand = brand;
        entry.Icon = icon;
        entry.LinkLabel = linkLabel;
        entry.LinkUrl = linkUrl;
        entry.SecondaryLinkLabel = secondaryLabel;
        entry.SecondaryLinkUrl = secondaryUrl;
        entry.ImageId = request.ImageId;
        entry.CtaTitle = ctaTitle;
        entry.CtaBody = ctaBody;
        entry.SeoTitle = seoTitle;
        entry.SeoDescription = seoDescription;
        entry.SortOrder = request.SortOrder;
        entry.IsPublished = request.IsPublished;
        entry.PublishAt = request.PublishAt is { } at ? DateTime.SpecifyKind(at.ToUniversalTime(), DateTimeKind.Utc) : null;
    }

    private void AddRevision(SiteEntry entry) => db.Revisions.Add(RevisionOf(entry, clock.GetUtcNow().UtcDateTime));

    /// <summary>The entry as it is now, kept before a change so the admin can bring it back.</summary>
    internal static SiteRevision RevisionOf(SiteEntry entry, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        EntryId = entry.Id,
        Version = entry.Version,
        Snapshot = JsonSerializer.Serialize(SiteEntrySnapshot.From(entry), SnapshotJson),
        CreatedAt = now
    };

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw SiteRequestException.Conflict("Bu içerik siz düzenlerken değişti. Sayfayı yenileyip yeniden deneyin.");
        }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("23505") == true
            || exception.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            throw new SiteRequestException(400, "İçerik kaydedilemedi; işaretli alanları düzeltin.",
                new Dictionary<string, string[]> { ["slug"] = ["Bu kısa ad kullanılıyor."] });
        }
    }

    private IQueryable<SiteEntry> Published(string site = SiteRules.Eduivme)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return db.Entries.AsNoTracking().Where(item => item.Site == site && item.IsPublished
            && (item.PublishAt == null || item.PublishAt <= now) && (item.EndsAt == null || item.EndsAt > now));
    }

    private static List<SitePublicEntry> Of(IEnumerable<SiteEntry> entries, string kind) =>
        entries.Where(item => item.Kind == kind).OrderBy(item => item.SortOrder).ThenBy(item => item.CreatedAt).Select(Public).ToList();

    private static string? ImageUrl(Guid? id) => id is { } value ? MediaPath + value : null;

    private static SitePublicEntry Public(SiteEntry item) => new(
        item.Id, item.Kind, item.Slug, item.Title, item.Subtitle, item.Body, item.Highlights, item.Brand, item.Icon,
        item.LinkLabel, item.LinkUrl, item.SecondaryLinkLabel, item.SecondaryLinkUrl, ImageUrl(item.ImageId),
        item.CtaTitle, item.CtaBody, item.SeoTitle, item.SeoDescription, item.Category, item.Audience, item.Author,
        item.PublishAt ?? item.CreatedAt);

    private static SiteEntryView View(SiteEntry item) => new(
        item.Id, item.Kind, item.Group, item.Slug, item.Title, item.Subtitle, item.Body, item.Highlights, item.Brand, item.Icon,
        item.LinkLabel, item.LinkUrl, item.SecondaryLinkLabel, item.SecondaryLinkUrl, item.ImageId, ImageUrl(item.ImageId),
        item.CtaTitle, item.CtaBody, item.SeoTitle, item.SeoDescription, item.SortOrder, item.IsPublished, item.PublishAt,
        item.Version, item.CreatedAt, item.UpdatedAt, item.Site, item.Category, item.Audience, item.EndsAt, item.Author);

    private static string? Optional(string? value, int max, string field, out SiteRequestException? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length <= max) return trimmed;
        error = Invalid(field, $"En çok {max} karakter olabilir.");
        return null;
    }

    private static SiteRequestException Invalid(string field, string message) =>
        new(400, "İçerik kaydedilemedi; işaretli alanları düzeltin.", new Dictionary<string, string[]> { [field] = [message] });

    private static string? DetectImage(byte[] content)
    {
        if (content.Length >= 8 && content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47
            && content[4] == 0x0D && content[5] == 0x0A && content[6] == 0x1A && content[7] == 0x0A) return "image/png";
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF) return "image/jpeg";
        if (content.Length >= 12 && content[0] == (byte)'R' && content[1] == (byte)'I' && content[2] == (byte)'F' && content[3] == (byte)'F'
            && content[8] == (byte)'W' && content[9] == (byte)'E' && content[10] == (byte)'B' && content[11] == (byte)'P') return "image/webp";
        return null;
    }
}

/// <summary>The editable fields of an entry, as kept in a revision.</summary>
public sealed record SiteEntrySnapshot(
    string Group, string? Slug, string Title, string? Subtitle, string Body, List<string> Highlights, string? Brand, string? Icon,
    string? LinkLabel, string? LinkUrl, string? SecondaryLinkLabel, string? SecondaryLinkUrl, Guid? ImageId,
    string? CtaTitle, string? CtaBody, string? SeoTitle, string? SeoDescription, int SortOrder, bool IsPublished, DateTime? PublishAt,
    string? Category = null, string? Audience = null, DateTime? EndsAt = null, string? Author = null)
{
    public static SiteEntrySnapshot From(SiteEntry item) => new(
        item.Group, item.Slug, item.Title, item.Subtitle, item.Body, [.. item.Highlights], item.Brand, item.Icon, item.LinkLabel, item.LinkUrl,
        item.SecondaryLinkLabel, item.SecondaryLinkUrl, item.ImageId, item.CtaTitle, item.CtaBody, item.SeoTitle, item.SeoDescription,
        item.SortOrder, item.IsPublished, item.PublishAt, item.Category, item.Audience, item.EndsAt, item.Author);

    public SiteEntryRequest ToRequest(string kind, int version, string site) => new(
        kind, Group, Slug, Title, Subtitle, Body, Highlights, Brand, Icon, LinkLabel, LinkUrl, SecondaryLinkLabel, SecondaryLinkUrl,
        ImageId, CtaTitle, CtaBody, SeoTitle, SeoDescription, SortOrder, IsPublished, PublishAt, version, site, Category, Audience, EndsAt, Author);
}
