using FluentAssertions;
using SpeedReading.Application.Content;

namespace SpeedReading.Application.UnitTests;

public sealed class CmsPageSlugPolicyTests
{
    [Theory]
    [InlineData("privacy")]
    [InlineData(" Privacy ")]
    [InlineData("KVKK")]
    [InlineData("kvkk")]
    public void SharedLegalPageSlugsAreReservedFromProductCms(string slug) =>
        CmsPageSlugPolicy.IsSharedLegalPageSlug(slug).Should().BeTrue();

    [Theory]
    [InlineData("about-us")]
    [InlineData("privacy-center")]
    [InlineData(null)]
    public void OtherCmsSlugsRemainAvailable(string? slug) =>
        CmsPageSlugPolicy.IsSharedLegalPageSlug(slug).Should().BeFalse();
}
