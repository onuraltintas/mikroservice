using System.Text.RegularExpressions;
using Identity.Application.LegalPages;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

public sealed class PlatformLegalPagesService(IdentityDbContext db) : IPlatformLegalPages
{
    private static readonly Regex SlugPattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private const int MaxContentLength = 200_000;

    public async Task<PlatformLegalPageDto?> GetPublishedAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = NormalizeSlug(slug);
        var page = await db.PlatformLegalPages.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Slug == normalizedSlug && item.IsPublished && item.ArchivedAt == null, cancellationToken);
        return page is null ? null : ToDto(page);
    }

    public async Task<IReadOnlyList<PlatformLegalPageDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await db.PlatformLegalPages.AsNoTracking()
            .OrderBy(item => item.ArchivedAt != null)
            .ThenBy(item => item.Slug)
            .ToListAsync(cancellationToken))
        .Select(ToDto)
        .ToArray();

    public async Task<PlatformLegalPageDto?> GetAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = NormalizeSlug(slug);
        var page = await db.PlatformLegalPages.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);
        return page is null ? null : ToDto(page);
    }

    public async Task<PlatformLegalPageDto> UpsertAsync(
        string slug,
        PlatformLegalPageUpdateRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = NormalizeSlug(slug);
        if (actorId == Guid.Empty)
            throw new ArgumentException("Güncelleyen yönetici kimliği gereklidir.", nameof(actorId));

        var title = request.Title?.Trim();
        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            throw new ArgumentException("Başlık zorunludur ve 200 karakteri aşamaz.", nameof(request));
        if (string.IsNullOrWhiteSpace(content) || content.Length > MaxContentLength)
            throw new ArgumentException($"Yasal metin zorunludur ve {MaxContentLength} karakteri aşamaz.", nameof(request));
        if (request.IsPublished && IsStarterDraftContent(content))
            throw new InvalidOperationException("Başlangıç taslağı ve yer tutucular kaldırılmadan yasal belge yayımlanamaz.");

        var now = DateTime.UtcNow;
        var page = await db.PlatformLegalPages.SingleOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);
        if (page?.ArchivedAt is not null)
            throw new InvalidOperationException("Arşivlenmiş yasal belge düzenlenmeden önce geri yüklenmelidir.");

        if (page is null)
        {
            page = new PlatformLegalPage
            {
                Slug = normalizedSlug,
                Title = title,
                Content = content,
                IsPublished = request.IsPublished,
                Version = 1,
                CreatedBy = actorId,
                CreatedAt = now
            };
            db.PlatformLegalPages.Add(page);
        }
        else
        {
            if (page.Title == title && page.Content == content && page.IsPublished == request.IsPublished)
                return ToDto(page);

            AddRevision(page);
            page.Title = title;
            page.Content = content;
            page.IsPublished = request.IsPublished;
            page.Version++;
            page.UpdatedBy = actorId;
            page.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(page);
    }

    public async Task<int> CreateStarterDraftsAsync(Guid actorId, CancellationToken cancellationToken = default)
    {
        EnsureActor(actorId);
        var existingSlugs = await db.PlatformLegalPages.AsNoTracking()
            .Select(page => page.Slug)
            .ToHashSetAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var missingDrafts = PlatformLegalStarterDrafts.All
            .Where(draft => !existingSlugs.Contains(draft.Slug))
            .Select(draft => new PlatformLegalPage
            {
                Slug = draft.Slug,
                Title = draft.Title,
                Content = draft.Content,
                IsPublished = false,
                Version = 1,
                CreatedBy = actorId,
                CreatedAt = now
            })
            .ToArray();

        if (missingDrafts.Length == 0)
            return 0;

        db.PlatformLegalPages.AddRange(missingDrafts);
        await db.SaveChangesAsync(cancellationToken);
        return missingDrafts.Length;
    }

    public async Task<PlatformLegalPageDto?> ArchiveAsync(
        string slug,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = NormalizeSlug(slug);
        EnsureActor(actorId);
        var page = await db.PlatformLegalPages.SingleOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);
        if (page is null) return null;
        if (page.ArchivedAt is not null) return ToDto(page);

        AddRevision(page);
        page.IsPublished = false;
        page.ArchivedAt = DateTime.UtcNow;
        page.Version++;
        page.UpdatedBy = actorId;
        page.UpdatedAt = page.ArchivedAt;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(page);
    }

    public async Task<PlatformLegalPageDto?> RestoreAsync(
        string slug,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = NormalizeSlug(slug);
        EnsureActor(actorId);
        var page = await db.PlatformLegalPages.SingleOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);
        if (page is null) return null;
        if (page.ArchivedAt is null) return ToDto(page);

        AddRevision(page);
        page.ArchivedAt = null;
        page.IsPublished = false;
        page.Version++;
        page.UpdatedBy = actorId;
        page.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(page);
    }

    public async Task<IReadOnlyList<PlatformLegalPageRevisionDto>> GetRevisionsAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = NormalizeSlug(slug);
        var pageId = await db.PlatformLegalPages.AsNoTracking()
            .Where(item => item.Slug == normalizedSlug)
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (pageId is null)
            return [];

        return (await db.PlatformLegalPageRevisions.AsNoTracking()
            .Where(item => item.PageId == pageId.Value)
            .OrderByDescending(item => item.Version)
            .ToListAsync(cancellationToken))
            .Select(item => new PlatformLegalPageRevisionDto(
                item.Id, item.Slug, item.Title, item.Content, item.Version, item.IsArchived, item.CreatedAt, item.CreatedBy))
            .ToArray();
    }

    private void AddRevision(PlatformLegalPage page) => db.PlatformLegalPageRevisions.Add(new PlatformLegalPageRevision
    {
        PageId = page.Id,
        Slug = page.Slug,
        Title = page.Title,
        Content = page.Content,
        IsPublished = page.IsPublished,
        IsArchived = page.ArchivedAt is not null,
        Version = page.Version,
        CreatedBy = page.UpdatedBy ?? page.CreatedBy,
        CreatedAt = page.UpdatedAt ?? page.CreatedAt
    });

    private static void EnsureActor(Guid actorId)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("İşlemi yapan yönetici kimliği gereklidir.", nameof(actorId));
    }

    private static bool IsStarterDraftContent(string content) =>
        content.Contains("[TASLAK — HUKUK İNCELEMESİ", StringComparison.Ordinal)
        || content.Contains("[[", StringComparison.Ordinal);

    private static string NormalizeSlug(string? slug)
    {
        var normalized = slug?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 80 || !SlugPattern.IsMatch(normalized))
            throw new ArgumentException("Yasal sayfa adresi küçük harf, rakam ve tire içermelidir.", nameof(slug));
        return normalized;
    }

    private static PlatformLegalPageDto ToDto(PlatformLegalPage page) => new(
        page.Slug, page.Title, page.Content, page.IsPublished, page.ArchivedAt is not null, page.ArchivedAt, page.Version,
        page.CreatedAt, page.UpdatedAt, page.UpdatedBy);
}
