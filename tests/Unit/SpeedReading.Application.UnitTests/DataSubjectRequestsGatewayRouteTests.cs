using System.Text.Json;
using FluentAssertions;

namespace SpeedReading.Application.UnitTests;

public sealed class DataSubjectRequestsGatewayRouteTests
{
    [Fact]
    public void Data_subject_request_api_is_forwarded_to_identity_without_bypassing_authorization()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(GetGatewaySettingsPath()));
        var route = document.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Routes")
            .GetProperty("identity-data-subject-requests-route");

        route.GetProperty("ClusterId").GetString().Should().Be("identity-cluster");
        route.GetProperty("Match").GetProperty("Path").GetString()
            .Should().Be("/api/data-subject-requests/{**catch-all}");
        route.TryGetProperty("AuthorizationPolicy", out var authorizationPolicy)
            .Should().BeFalse();
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
