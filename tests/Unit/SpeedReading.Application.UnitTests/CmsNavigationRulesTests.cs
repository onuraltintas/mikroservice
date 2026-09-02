using FluentAssertions;
using SpeedReading.Application.Content;

namespace SpeedReading.Application.UnitTests;

public sealed class CmsNavigationRulesTests
{
    [Fact]
    public void Normalizes_menu_and_internal_url_values()
    {
        var request = new CmsNavigationItemRequest(
            " ",
            " Blog ",
            " /blog ",
            " #featured ",
            null,
            4,
            true,
            false);

        var normalized = CmsNavigationRules.Normalize(request);

        normalized.Menu.Should().Be("Main");
        normalized.Label.Should().Be("Blog");
        normalized.Url.Should().Be("/blog");
        normalized.Fragment.Should().Be("featured");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("//evil.example")]
    [InlineData("ftp://files.example")]
    public void Rejects_unsafe_navigation_urls(string url)
    {
        var request = new CmsNavigationItemRequest("Main", "Link", url, null, null, 0, true, false);

        var action = () => CmsNavigationRules.Normalize(request);

        action.Should().Throw<ArgumentException>();
    }
}
