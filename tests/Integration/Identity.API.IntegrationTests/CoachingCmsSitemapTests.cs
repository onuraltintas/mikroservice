using Coaching.API.Controllers;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Management;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

// Roadmap O11: the public site's sitemap lists the published pages and blog posts with their last change.
public sealed partial class CoachingCmsAndSubscriptionServiceTests
{
    [Fact]
    public async Task TheSitemapListsOnlyPublishedPagesAndPosts()
    {
        await using var db = CreateDbContext();
        var updated = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
        db.CoachingCmsEntries.AddRange(
            new CoachingCmsEntry { Kind = "Page", Slug = "kurumlar", Title = "Kurumlar", IsPublished = true, UpdatedAt = updated },
            new CoachingCmsEntry { Kind = "Page", Slug = "taslak", Title = "Taslak", IsPublished = false },
            new CoachingCmsEntry { Kind = "Blog", Slug = "lgs-hazirlik", Title = "LGS", IsPublished = true, PublishedAt = updated.AddDays(-3) },
            new CoachingCmsEntry { Kind = "Blog", Slug = "gelecek", Title = "Gelecek", IsPublished = true, ScheduledPublishAt = DateTime.UtcNow.AddDays(2) },
            new CoachingCmsEntry { Kind = "Block", Slug = "hero", Title = "Hero", Group = "HomePage", IsPublished = true });
        await db.SaveChangesAsync();

        var entries = await new CoachingCmsService(db, new MemoryCmsMediaStorage()).GetSitemapAsync(default);

        entries.Select(entry => (entry.Kind, entry.Slug)).Should().BeEquivalentTo(new[] { ("Page", "kurumlar"), ("Blog", "lgs-hazirlik") });
        entries.Single(entry => entry.Slug == "kurumlar").LastModified.Should().Be(updated);
        entries.Single(entry => entry.Slug == "lgs-hazirlik").LastModified.Should().Be(updated.AddDays(-3));
        typeof(CoachingCmsController).GetMethod("GetSitemap").Should().NotBeNull();
    }
}
