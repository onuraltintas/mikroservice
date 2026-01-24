using Testcontainers.Redis;
using Xunit;

namespace Shared.IntegrationTests.Fixtures;

/// <summary>
/// Redis test container fixture for integration tests.
/// Provides a real Redis instance running in Docker.
/// </summary>
public class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container;

    public RedisFixture()
    {
        _container = new RedisBuilder()
            .WithImage("redis:7.2-alpine")
            .WithCleanUp(true)
            .Build();
    }

    /// <summary>
    /// Gets the connection string for Redis
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Gets the container host
    /// </summary>
    public string Host => _container.Hostname;

    /// <summary>
    /// Gets the Redis port (6379)
    /// </summary>
    public int Port => _container.GetMappedPublicPort(6379);

    /// <summary>
    /// Starts the Redis container
    /// </summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    /// <summary>
    /// Stops and disposes the Redis container
    /// </summary>
    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}
