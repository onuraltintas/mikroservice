using FluentAssertions;
using Identity.Application.LegalPages;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class RegistrationLegalConsentServiceTests
{
    [Fact]
    public async Task ValidateRequiresPublishedCentralDocumentsBeforeUserProvisioning()
    {
        await using var db = CreateDbContext();
        var pages = new PlatformLegalPagesService(db);
        var service = new RegistrationLegalConsentService(db, pages);
        var acceptances = new[]
        {
            new LegalPageAcceptance("privacy", 1),
            new LegalPageAcceptance("kvkk", 1),
            new LegalPageAcceptance("coaching-terms", 1)
        };

        var result = await service.ValidateAsync(PlatformProduct.Coaching, acceptances);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.LegalDocumentUnavailable");
        (await db.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TrackAcceptedDocumentsPersistsImmutableProductAndVersionEvidence()
    {
        await using var db = CreateDbContext();
        var pages = new PlatformLegalPagesService(db);
        var adminId = Guid.NewGuid();
        foreach (var slug in RegistrationLegalConsentPolicy.RequiredSlugs(PlatformProduct.Coaching))
            await pages.UpsertAsync(slug, new(slug, "Hukukça incelenecek metin", true), adminId);

        var userId = Guid.NewGuid();
        db.Users.Add(User.Create(userId, "student@example.test"));
        await db.SaveChangesAsync();
        var service = new RegistrationLegalConsentService(db, pages);
        var consentResult = await service.ValidateAsync(
            PlatformProduct.Coaching,
            RegistrationLegalConsentPolicy.RequiredSlugs(PlatformProduct.Coaching)
                .Select(slug => new LegalPageAcceptance(slug, 1)));

        service.TrackAcceptedDocuments(userId, PlatformProduct.Coaching, consentResult.Value, "password");
        await db.SaveChangesAsync();

        var recorded = await db.RegistrationLegalDocumentAcceptances
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.DocumentSlug)
            .ToListAsync();
        recorded.Should().HaveCount(3);
        recorded.Select(item => item.Product).Distinct().Should().ContainSingle().Which.Should().Be(PlatformProduct.Coaching);
        recorded.Should().Contain(item => item.DocumentSlug == "coaching-terms" && item.Action == RegistrationLegalDocumentAction.Accepted);
        recorded.Should().Contain(item => item.DocumentSlug == "kvkk" && item.Action == RegistrationLegalDocumentAction.Acknowledged);
        recorded.Should().OnlyContain(item => item.DocumentVersion == 1 && item.RegistrationMethod == "password");

        recorded[0].DocumentVersion = 2;
        var mutation = async () => await db.SaveChangesAsync();
        await mutation.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Registration legal document acceptance records are append-only.");
    }

    private static IdentityDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
