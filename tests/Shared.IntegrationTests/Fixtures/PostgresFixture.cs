using Testcontainers.PostgreSql;
using Xunit;

namespace Shared.IntegrationTests.Fixtures;

/// <summary>
/// PostgreSQL test container fixture for integration tests.
/// Provides a real PostgreSQL instance running in Docker.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public PostgresFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("test_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();
    }

    /// <summary>
    /// Gets the connection string for the PostgreSQL container
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Gets the container host
    /// </summary>
    public string Host => _container.Hostname;

    /// <summary>
    /// Gets the container port
    /// </summary>
    public int Port => _container.GetMappedPublicPort(5432);

    /// <summary>
    /// Starts the PostgreSQL container
    /// </summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    /// <summary>
    /// Stops and disposes the PostgreSQL container
    /// </summary>
    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}
