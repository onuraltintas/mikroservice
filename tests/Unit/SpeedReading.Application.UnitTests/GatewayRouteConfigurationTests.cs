using System.Text.Json;
using FluentAssertions;

namespace SpeedReading.Application.UnitTests;

public sealed class GatewayRouteConfigurationTests
{
    [Theory]
    [InlineData("speed-reading-cms-public-route", "/api/speed-reading/cms/{**catch-all}")]
    [InlineData("speed-reading-navigation-public-route", "/api/speed-reading/cms/navigation")]
    [InlineData("speed-reading-sitemap-public-route", "/api/speed-reading/sitemap.xml")]
    [InlineData("speed-reading-subscription-plans-public-route", "/api/speed-reading/subscription-plans/{**catch-all}")]
    [InlineData("speed-reading-products-public-route", "/api/speed-reading/products/{**catch-all}")]
    [InlineData("coaching-cms-public-route", "/api/coaching/cms/{**catch-all}")]
    [InlineData("coaching-subscription-plans-public-route", "/api/coaching/subscription-plans/{**catch-all}")]
    [InlineData("coaching-bank-transfer-settings-public-route", "/api/coaching/subscriptions/bank-transfer-settings")]
    [InlineData("identity-platform-legal-pages-public-route", "/api/platform/legal-pages/{**catch-all}")]
    [InlineData("identity-platform-legal-pages-public-root-route", "/api/platform/legal-pages")]
    public void Public_content_and_pricing_routes_bypass_gateway_fallback_authorization(
        string routeName,
        string pathPattern)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(GetGatewaySettingsPath()));
        var route = document.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Routes")
            .GetProperty(routeName);

        route.GetProperty("AuthorizationPolicy").GetString().Should().Be("Anonymous");
        route.GetProperty("Match").GetProperty("Path").GetString().Should().Be(pathPattern);
    }

    [Fact]
    public void Speed_reading_caddy_forwards_identity_auth_paths_before_api_fallback()
    {
        var caddy = File.ReadAllText(GetSpeedReadingCaddyPath());
        var identityRouteIndex = caddy.IndexOf("@identityAuth path /api/auth /api/auth/*", StringComparison.Ordinal);
        identityRouteIndex.Should().BeGreaterThanOrEqualTo(0);

        if (identityRouteIndex < 0)
        {
            return;
        }

        var identityHandlerIndex = caddy.IndexOf("handle @identityAuth", identityRouteIndex, StringComparison.Ordinal);
        var gatewayProxyIndex = caddy.IndexOf("reverse_proxy api-gateway:8080", identityHandlerIndex, StringComparison.Ordinal);
        var blockedApiIndex = caddy.IndexOf("@blockedApi path /api/*", StringComparison.Ordinal);

        identityHandlerIndex.Should().BeGreaterThan(identityRouteIndex);
        gatewayProxyIndex.Should().BeGreaterThan(identityHandlerIndex);
        gatewayProxyIndex.Should().BeLessThan(blockedApiIndex);
    }

    [Fact]
    public void Speed_reading_caddy_forwards_versioned_identity_users_paths_before_api_fallback()
    {
        var caddy = File.ReadAllText(GetSpeedReadingCaddyPath());
        var versionedUsersPathIndex = caddy.IndexOf(
            "/api/v1/users /api/v1/users/*",
            StringComparison.Ordinal);
        var versionedHandlerIndex = caddy.IndexOf("handle @versionedApi", StringComparison.Ordinal);
        var blockedApiIndex = caddy.IndexOf("@blockedApi path /api/*", StringComparison.Ordinal);

        versionedUsersPathIndex.Should().BeGreaterThanOrEqualTo(0);
        versionedHandlerIndex.Should().BeGreaterThan(versionedUsersPathIndex);
        versionedHandlerIndex.Should().BeLessThan(blockedApiIndex);
    }

    [Fact]
    public void Speed_reading_caddy_forwards_versioned_coaching_admin_paths_before_api_fallback()
    {
        var caddy = File.ReadAllText(GetSpeedReadingCaddyPath());
        var coachingPathIndex = caddy.IndexOf(
            "@versionedCoachingAdmin path /api/v1/coaching-admin /api/v1/coaching-admin/*",
            StringComparison.Ordinal);
        var coachingHandlerIndex = caddy.IndexOf("handle @versionedCoachingAdmin", StringComparison.Ordinal);
        var blockedApiIndex = caddy.IndexOf("@blockedApi path /api/*", StringComparison.Ordinal);

        coachingPathIndex.Should().BeGreaterThanOrEqualTo(0);
        coachingHandlerIndex.Should().BeGreaterThan(coachingPathIndex);
        coachingHandlerIndex.Should().BeLessThan(blockedApiIndex);
    }

    [Fact]
    public void Coaching_agreement_route_forwards_to_the_coaching_cluster()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(GetGatewaySettingsPath()));
        var route = document.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Routes")
            .GetProperty("coaching-agreements-route");

        route.GetProperty("ClusterId").GetString().Should().Be("coaching-cluster");
        route.GetProperty("Match").GetProperty("Path").GetString()
            .Should().Be("/api/coaching-agreements/{**catch-all}");
    }

    [Fact]
    public void Coaching_subscription_and_cms_route_forwards_to_the_coaching_cluster()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(GetGatewaySettingsPath()));
        var route = document.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Routes")
            .GetProperty("coaching-route");

        route.GetProperty("ClusterId").GetString().Should().Be("coaching-cluster");
        route.GetProperty("Match").GetProperty("Path").GetString()
            .Should().Be("/api/coaching/{**catch-all}");
    }

    [Fact]
    public void Speed_reading_caddy_forwards_coaching_agreements_before_api_fallback()
    {
        var caddy = File.ReadAllText(GetSpeedReadingCaddyPath());
        var agreementPathIndex = caddy.IndexOf(
            "@coachingAgreements path /api/coaching-agreements /api/coaching-agreements/*",
            StringComparison.Ordinal);
        var agreementHandlerIndex = caddy.IndexOf(
            "handle @coachingAgreements",
            agreementPathIndex,
            StringComparison.Ordinal);
        var blockedApiIndex = caddy.IndexOf("@blockedApi path /api/*", StringComparison.Ordinal);

        agreementPathIndex.Should().BeGreaterThanOrEqualTo(0);
        agreementHandlerIndex.Should().BeGreaterThan(agreementPathIndex);
        agreementHandlerIndex.Should().BeLessThan(blockedApiIndex);
    }

    [Theory]
    [InlineData("speed-reading-admin-audit-route", "/api/admin-audit/speed-reading")]
    [InlineData("speed-reading-admin-audit-facets-route", "/api/admin-audit/speed-reading/facets")]
    public void Speed_reading_admin_audit_routes_forward_to_the_speed_reading_cluster(
        string routeName,
        string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(GetGatewaySettingsPath()));
        var route = document.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Routes")
            .GetProperty(routeName);

        route.GetProperty("ClusterId").GetString().Should().Be("speed-reading-cluster");
        route.GetProperty("Match").GetProperty("Path").GetString().Should().Be(path);
    }

    private static string GetGatewaySettingsPath()
    {
        return Path.Combine(GetRepositoryRoot(), "services", "api-gateway", "appsettings.json");
    }

    private static string GetSpeedReadingCaddyPath()
    {
        return Path.Combine(GetRepositoryRoot(), "infrastructure", "caddy", "Caddyfile.speed-reading.litespeed");
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory!.FullName;
    }
}
