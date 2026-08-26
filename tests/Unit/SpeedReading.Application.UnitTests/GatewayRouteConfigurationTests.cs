using System.Text.Json;
using FluentAssertions;

namespace SpeedReading.Application.UnitTests;

public sealed class GatewayRouteConfigurationTests
{
    [Theory]
    [InlineData("speed-reading-cms-public-route", "/api/speed-reading/cms/{**catch-all}")]
    [InlineData("speed-reading-subscription-plans-public-route", "/api/speed-reading/subscription-plans/{**catch-all}")]
    public void Public_speed_reading_routes_bypass_gateway_fallback_authorization(
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

    private static string GetGatewaySettingsPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "services", "api-gateway", "appsettings.json");
    }
}
