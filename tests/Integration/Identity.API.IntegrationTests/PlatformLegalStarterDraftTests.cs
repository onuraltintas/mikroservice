using FluentAssertions;
using Identity.Application.LegalPages;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class PlatformLegalStarterDraftTests
{
    [Fact]
    public async Task CreateStarterDraftsAddsEditableUnpublishedTemplatesWithoutOverwritingExistingDocuments()
    {
        await using var db = CreateDbContext();
        var service = new PlatformLegalPagesService(db);
        var actorId = Guid.NewGuid();

        var createdCount = await service.CreateStarterDraftsAsync(actorId);
        var drafts = await service.GetAllAsync();

        createdCount.Should().Be(PlatformLegalStarterDrafts.All.Count);
        drafts.Should().HaveCount(PlatformLegalStarterDrafts.All.Count);
        drafts.Should().OnlyContain(page => !page.IsPublished && !page.IsArchived && page.Version == 1);
        drafts.Should().OnlyContain(page => page.Content.Contains("[TASLAK — HUKUK İNCELEMESİ", StringComparison.Ordinal));
        drafts.Select(page => page.Slug).Should().Contain([
            "privacy", "kvkk", "cookies", "coaching-terms", "speed-reading-terms",
            "coaching-newsletter-consent", "speed-reading-newsletter-consent"]);

        var updated = await service.UpsertAsync("privacy", new("Gizlilik", "Kullanıcının düzenlediği taslak", false), actorId);
        var secondRunCount = await service.CreateStarterDraftsAsync(actorId);

        secondRunCount.Should().Be(0);
        (await service.GetAsync("privacy"))!.Content.Should().Be(updated.Content);
    }

    [Fact]
    public async Task UpsertRefusesToPublishUnreviewedStarterDraftsButAllowsEditedText()
    {
        await using var db = CreateDbContext();
        var service = new PlatformLegalPagesService(db);
        var actorId = Guid.NewGuid();
        await service.CreateStarterDraftsAsync(actorId);
        var draft = (await service.GetAsync("privacy"))!;

        var publishDraft = async () => await service.UpsertAsync(
            draft.Slug,
            new(draft.Title, draft.Content, true),
            actorId);
        await publishDraft.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Başlangıç taslağı ve yer tutucular kaldırılmadan yasal belge yayımlanamaz.");

        var published = await service.UpsertAsync(
            draft.Slug,
            new(draft.Title, "Hukuk incelemesinden geçirilip işletme bilgileri eklenmiş metin.", true),
            actorId);
        published.IsPublished.Should().BeTrue();
    }

    private static IdentityDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
