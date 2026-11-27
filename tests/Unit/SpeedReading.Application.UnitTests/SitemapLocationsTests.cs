using FluentAssertions;
using SpeedReading.API.Controllers;

namespace SpeedReading.Application.UnitTests;

public sealed class SitemapLocationsTests
{
    private const string Site = "https://masterhizliokuma.com";

    [Fact]
    public void Lists_the_static_public_pages_on_the_canonical_host()
    {
        var locations = SitemapController.BuildLocations(Site + "/", Array.Empty<string>(), Array.Empty<string>());

        locations.Should().Equal(SitemapController.StaticPaths.Select(path => Site + path));
        locations.Should().Contain(new[] { Site + "/", Site + "/sss", Site + "/blog", Site + "/legal/privacy" });
        locations.Should().OnlyContain(location => location.StartsWith(Site + "/", StringComparison.Ordinal));
    }

    [Fact]
    public void Cms_pages_are_listed_at_their_client_route_and_posts_under_blog()
    {
        var locations = SitemapController.BuildLocations(Site, new[] { "help", "/nasil-calisir/" }, new[] { "hizli-okuma-nedir" });

        locations.Should().Contain(new[] { Site + "/help", Site + "/nasil-calisir", Site + "/blog/hizli-okuma-nedir" });
        locations.Should().NotContain(location => location.Contains("/pages/", StringComparison.Ordinal));
    }

    [Fact]
    public void Slugs_that_collide_with_app_routes_or_repeat_are_left_out()
    {
        var locations = SitemapController.BuildLocations(Site, new[] { "sss", "auth", "Blog", "", "a/b", "help", "help" }, Array.Empty<string>());

        locations.Count(location => location == Site + "/sss").Should().Be(1);
        locations.Should().NotContain(new[] { Site + "/auth", Site + "/Blog", Site + "/a%2Fb", Site + "/a/b" });
        locations.Count(location => location == Site + "/help").Should().Be(1);
    }
}
