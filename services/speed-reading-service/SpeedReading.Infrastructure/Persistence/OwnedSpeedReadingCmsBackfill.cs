using Microsoft.EntityFrameworkCore;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingCmsBackfill(SpeedReadingDbContext legacy, OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedCmsBackfillResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var sourceContent = await legacy.ContentBlocks.AsNoTracking().ToListAsync(cancellationToken);
        var sourcePages = await legacy.Pages.AsNoTracking().ToListAsync(cancellationToken);
        var sourcePosts = await legacy.BlogPosts.AsNoTracking().ToListAsync(cancellationToken);
        var sourceMessages = await legacy.ContactMessages.AsNoTracking().ToListAsync(cancellationToken);
        var sourceSubscribers = await legacy.NewsletterSubscribers.AsNoTracking().ToListAsync(cancellationToken);
        var target = (ISpeedReadingDataContext)owned;
        var existingContent = await target.ContentBlocks.IgnoreQueryFilters().Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var existingPages = await target.Pages.IgnoreQueryFilters().Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var existingPosts = await target.BlogPosts.IgnoreQueryFilters().Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var existingMessages = await target.ContactMessages.IgnoreQueryFilters().Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var existingSubscribers = await target.NewsletterSubscribers.IgnoreQueryFilters().Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var imported = 0;
        foreach (var item in sourceContent.Where(item => !existingContent.Contains(item.Id))) { target.ContentBlocks.Add(item); imported++; }
        foreach (var item in sourcePages.Where(item => !existingPages.Contains(item.Id))) { target.Pages.Add(item); imported++; }
        foreach (var item in sourcePosts.Where(item => !existingPosts.Contains(item.Id))) { target.BlogPosts.Add(item); imported++; }
        foreach (var item in sourceMessages.Where(item => !existingMessages.Contains(item.Id))) { target.ContactMessages.Add(item); imported++; }
        foreach (var item in sourceSubscribers.Where(item => !existingSubscribers.Contains(item.Id))) { target.NewsletterSubscribers.Add(item); imported++; }
        if (imported > 0) await owned.SaveChangesAsync(cancellationToken);
        return new OwnedCmsBackfillResult(sourceContent.Count + sourcePages.Count + sourcePosts.Count + sourceMessages.Count + sourceSubscribers.Count, imported);
    }
}

public sealed record OwnedCmsBackfillResult(int SourceCount, int ImportedCount);
