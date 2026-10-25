namespace EduPlatform.Gateway.UnitTests;

/// <summary>
/// Static checks on the gateway image and compose wiring. They guard the operational
/// contract (non-root user, writable key volume) that cannot be exercised in-process.
/// </summary>
public sealed class GatewayDeploymentConfigurationTests
{
    [Fact]
    public void Gateway_image_runs_as_the_non_root_app_user()
    {
        var dockerfile = ReadRepositoryFile("services", "api-gateway", "Dockerfile");
        var finalStage = dockerfile[dockerfile.LastIndexOf("FROM ", StringComparison.Ordinal)..];

        finalStage.Should().Contain("USER $APP_UID");
        finalStage.IndexOf("USER $APP_UID", StringComparison.Ordinal)
            .Should().BeGreaterThan(finalStage.IndexOf("chown", StringComparison.Ordinal),
                "the key directory must be prepared before dropping root");
        finalStage.Should().Contain("/var/lib/eduplatform/data-protection-keys");
    }

    [Fact]
    public void Compose_fixes_existing_key_volume_ownership_before_the_gateway_starts()
    {
        var compose = ReadRepositoryFile("docker-compose.yml");
        var initStart = compose.IndexOf("\n  api-gateway-volume-permissions:", StringComparison.Ordinal);
        initStart.Should().BeGreaterThan(0);
        var initBlock = compose[initStart..compose.IndexOf("\n  api-gateway:", StringComparison.Ordinal)];

        initBlock.Should().Contain("user: \"0:0\"");
        initBlock.Should().Contain("1654:1654");
        initBlock.Should().Contain("gateway_data_protection:/var/lib/eduplatform/data-protection-keys");
        initBlock.Should().Contain("network_mode: none");
        initBlock.Should().Contain("restart: \"no\"");

        var gatewayBlock = compose[compose.IndexOf("\n  api-gateway:", StringComparison.Ordinal)..];
        gatewayBlock.Should().Contain("api-gateway-volume-permissions:");
        gatewayBlock.Should().Contain("condition: service_completed_successfully");
    }

    internal static string ReadRepositoryFile(params string[] segments) =>
        File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. segments]));

    internal static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EduPlatform.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
