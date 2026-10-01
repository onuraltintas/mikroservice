using Identity.Application.LegalPages;
using Identity.Domain.Enums;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class RegistrationLegalConsentPolicyTests
{
    [Theory]
    [InlineData(PlatformProduct.Coaching, "coaching-terms")]
    [InlineData(PlatformProduct.SpeedReading, "speed-reading-terms")]
    public void RequiredSlugs_AlwaysIncludeSharedNoticesAndProductSpecificTerms(
        PlatformProduct product,
        string expectedTermsSlug)
    {
        RegistrationLegalConsentPolicy.RequiredSlugs(product)
            .Should().Equal("privacy", "kvkk", expectedTermsSlug);
    }

    [Fact]
    public void Validate_RejectsMissingAcceptance()
    {
        var result = RegistrationLegalConsentPolicy.Validate(
            PlatformProduct.Coaching,
            [new("privacy", 2), new("kvkk", 3)],
            [Page("privacy", 2), Page("kvkk", 3), Page("coaching-terms", 1)]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.LegalAcceptanceRequired");
    }

    [Fact]
    public void Validate_RejectsStaleDocumentVersion()
    {
        var result = RegistrationLegalConsentPolicy.Validate(
            PlatformProduct.SpeedReading,
            [new("privacy", 2), new("kvkk", 3), new("speed-reading-terms", 1)],
            [Page("privacy", 2), Page("kvkk", 4), Page("speed-reading-terms", 1)]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.LegalAcceptanceRequired");
    }

    [Fact]
    public void Validate_RejectsUnpublishedRequiredDocument()
    {
        var result = RegistrationLegalConsentPolicy.Validate(
            PlatformProduct.Coaching,
            [new("privacy", 2), new("kvkk", 3), new("coaching-terms", 1)],
            [Page("privacy", 2), Page("kvkk", 3), Page("coaching-terms", 1, isPublished: false)]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.LegalDocumentUnavailable");
    }

    [Fact]
    public void Validate_RejectsDuplicateAndUnknownSlugs()
    {
        var duplicate = RegistrationLegalConsentPolicy.Validate(
            PlatformProduct.Coaching,
            [new("privacy", 2), new("privacy", 2), new("kvkk", 3), new("coaching-terms", 1)],
            [Page("privacy", 2), Page("kvkk", 3), Page("coaching-terms", 1)]);

        var unknown = RegistrationLegalConsentPolicy.Validate(
            PlatformProduct.Coaching,
            [new("privacy", 2), new("kvkk", 3), new("coaching-terms", 1), new("other", 9)],
            [Page("privacy", 2), Page("kvkk", 3), Page("coaching-terms", 1)]);

        duplicate.Error.Code.Should().Be("Auth.InvalidLegalAcceptance");
        unknown.Error.Code.Should().Be("Auth.InvalidLegalAcceptance");
    }

    [Fact]
    public void Validate_AcceptsAllCurrentPublishedRequiredVersions()
    {
        var submitted = new[] { new LegalPageAcceptance("privacy", 2), new LegalPageAcceptance("kvkk", 3), new LegalPageAcceptance("coaching-terms", 1) };
        var pages = new[] { Page("privacy", 2), Page("kvkk", 3), Page("coaching-terms", 1) };

        var result = RegistrationLegalConsentPolicy.Validate(PlatformProduct.Coaching, submitted, pages);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(page => page.Slug).Should().Equal("privacy", "kvkk", "coaching-terms");
    }

    private static PlatformLegalPageDto Page(string slug, int version, bool isPublished = true) => new(
        slug,
        slug,
        "Content",
        isPublished,
        false,
        null,
        version,
        DateTime.UtcNow,
        null,
        null);
}
