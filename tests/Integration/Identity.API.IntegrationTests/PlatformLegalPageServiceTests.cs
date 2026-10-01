using Identity.Application.LegalPages;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class PlatformLegalPageServiceTests
{
    [Fact]
    public async Task Upsert_ValidatesSlugAndRequiresARealDocumentBeforePublishing()
    {
        await using var db = CreateDbContext();
        var service = new PlatformLegalPagesService(db);

        Func<Task> invalidSlug = async () => { await service.UpsertAsync("../privacy", new("Gizlilik", "Metin", true), Guid.NewGuid()); };
        Func<Task> emptyContent = async () => { await service.UpsertAsync("privacy", new("Gizlilik", " ", true), Guid.NewGuid()); };

        await invalidSlug.Should().ThrowAsync<ArgumentException>();
        await emptyContent.Should().ThrowAsync<ArgumentException>();
        (await db.PlatformLegalPages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UpsertMaintainsVersionHistoryAndPublicReadHidesUnpublishedPages()
    {
        await using var db = CreateDbContext();
        var service = new PlatformLegalPagesService(db);
        var firstEditorId = Guid.NewGuid();
        var secondEditorId = Guid.NewGuid();

        var firstVersion = await service.UpsertAsync(
            "privacy",
            new PlatformLegalPageUpdateRequest("Gizlilik Politikası", "İlk onaylı metin.", true),
            firstEditorId);

        firstVersion.Version.Should().Be(1);
        (await service.GetPublishedAsync("privacy"))!.Content.Should().Be("İlk onaylı metin.");

        var secondVersion = await service.UpsertAsync(
            "privacy",
            new PlatformLegalPageUpdateRequest("Gizlilik Politikası", "Güncellenmiş metin.", true),
            secondEditorId);

        secondVersion.Version.Should().Be(2);
        var revision = (await service.GetRevisionsAsync("privacy")).Should().ContainSingle().Subject;
        revision.Version.Should().Be(1);
        revision.Content.Should().Be("İlk onaylı metin.");
        revision.CreatedBy.Should().Be(firstEditorId);
        revision.CreatedAt.Should().Be(firstVersion.CreatedAt);
        (await service.GetPublishedAsync("privacy"))!.Content.Should().Be("Güncellenmiş metin.");

        await service.UpsertAsync(
            "privacy",
            new PlatformLegalPageUpdateRequest("Gizlilik Politikası", "Güncellenmiş metin.", false),
            secondEditorId);
        (await service.GetPublishedAsync("privacy")).Should().BeNull();
    }

    [Fact]
    public async Task ArchiveAndRestorePreserveHistoryAndNeverRepublishAutomatically()
    {
        await using var db = CreateDbContext();
        var service = new PlatformLegalPagesService(db);
        var actorId = Guid.NewGuid();
        await service.UpsertAsync("privacy", new("Gizlilik", "Yayımlı içerik", true), actorId);

        var archived = await service.ArchiveAsync("privacy", actorId);

        archived.Should().NotBeNull();
        archived!.IsArchived.Should().BeTrue();
        archived.IsPublished.Should().BeFalse();
        archived.Version.Should().Be(2);
        (await service.GetPublishedAsync("privacy")).Should().BeNull();
        var revision = (await service.GetRevisionsAsync("privacy")).Should().ContainSingle().Subject;
        revision.Content.Should().Be("Yayımlı içerik");
        revision.Version.Should().Be(1);

        var restored = await service.RestoreAsync("privacy", actorId);

        restored.Should().NotBeNull();
        restored!.IsArchived.Should().BeFalse();
        restored.IsPublished.Should().BeFalse();
        restored.Version.Should().Be(3);
        (await service.GetPublishedAsync("privacy")).Should().BeNull();
    }

    [Fact]
    public async Task UpsertAcceptsNewSharedDocumentSlugs()
    {
        await using var db = CreateDbContext();
        var service = new PlatformLegalPagesService(db);

        var page = await service.UpsertAsync(
            "accessibility-policy",
            new("Erişilebilirlik", "Onaylı metin", true),
            Guid.NewGuid());

        page.Slug.Should().Be("accessibility-policy");
        (await service.GetPublishedAsync("accessibility-policy"))!.Content.Should().Be("Onaylı metin");
    }

    private static IdentityDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
