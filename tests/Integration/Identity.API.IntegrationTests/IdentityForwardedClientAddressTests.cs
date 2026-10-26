using FluentAssertions;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Identity records the caller address on refresh tokens, sessions and audit entries.
/// Behind the gateway that address must come from the gateway's X-Forwarded-For, and only
/// when the request arrives from a configured internal network.
/// </summary>
public sealed class IdentityForwardedClientAddressTests
{
    [Fact]
    public void Identity_trusts_only_x_forwarded_for_as_the_first_middleware()
    {
        var source = ReadRepositoryFile("services", "identity-service", "Identity.API", "Program.cs");
        var pipelineStart = source.IndexOf("var app = builder.Build();", StringComparison.Ordinal);
        var forwarded = source.IndexOf("app.UseTrustedForwardedHeaders(ForwardedHeaders.XForwardedFor);", StringComparison.Ordinal);
        var requestLogging = source.IndexOf("app.UseRequestLogging();", pipelineStart, StringComparison.Ordinal);

        pipelineStart.Should().BeGreaterThan(0);
        forwarded.Should().BeGreaterThan(pipelineStart);
        forwarded.Should().BeLessThan(requestLogging, "the logged and stored address must already be the client address");
        source.Should().NotContain("ForwardedHeaders.XForwardedProto", "the scheme Identity sees must not change");
    }

    [Fact]
    public void Compose_trusts_the_internal_docker_network_for_identity_by_default()
    {
        var compose = ReadRepositoryFile("docker-compose.yml");
        var start = compose.IndexOf("\n  identity-service:", StringComparison.Ordinal);
        var end = compose.IndexOf("\n  clamav:", start, StringComparison.Ordinal);
        var identity = compose[start..end];

        identity.Should().Contain("ForwardedHeaders__KnownNetworks=${SERVICE_TRUSTED_PROXY_NETWORKS:-172.16.0.0/12}");
        identity.Should().Contain("ForwardedHeaders__ForwardLimit=1");
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EduPlatform.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine([directory!.FullName, .. segments]));
    }
}
