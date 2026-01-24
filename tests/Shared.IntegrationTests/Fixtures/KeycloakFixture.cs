using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Shared.IntegrationTests.Fixtures;

/// <summary>
/// Keycloak test container fixture for integration tests.
/// Provides a real Keycloak instance running in Docker.
/// </summary>
public class KeycloakFixture : IAsyncLifetime
{
    private readonly IContainer _container;

    public KeycloakFixture()
    {
        _container = new ContainerBuilder()
            .WithImage("quay.io/keycloak/keycloak:23.0.0")
            .WithPortBinding(8080, true)
            .WithEnvironment("KEYCLOAK_ADMIN", "admin")
            .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin123")
            .WithEnvironment("KC_HTTP_ENABLED", "true")
            .WithEnvironment("KC_HOSTNAME_STRICT", "false")
            .WithCommand("start-dev")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPath("/health/ready")
                    .ForPort(8080)))
            .WithCleanUp(true)
            .Build();
    }

    /// <summary>
    /// Gets the Keycloak base URL
    /// </summary>
    public string BaseUrl => $"http://{_container.Hostname}:{_container.GetMappedPublicPort(8080)}";

    /// <summary>
    /// Gets the admin username
    /// </summary>
    public string AdminUsername => "admin";

    /// <summary>
    /// Gets the admin password
    /// </summary>
    public string AdminPassword => "admin123";

    /// <summary>
    /// Gets the container host
    /// </summary>
    public string Host => _container.Hostname;

    /// <summary>
    /// Gets the HTTP port
    /// </summary>
    public int Port => _container.GetMappedPublicPort(8080);

    /// <summary>
    /// Starts the Keycloak container
    /// </summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        
        // Wait additional time for Keycloak to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Stops and disposes the Keycloak container
    /// </summary>
    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}
