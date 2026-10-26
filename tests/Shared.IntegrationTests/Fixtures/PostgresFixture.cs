using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Shared.IntegrationTests.Fixtures;

/// <summary>
/// A disposable PostgreSQL database for integration tests: a Testcontainers instance, or,
/// when <c>IDENTITY_TEST_POSTGRES</c> names a server (machines without Docker), a fresh
/// database on that server that is dropped afterwards.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    public const string ExternalServerVariable = "IDENTITY_TEST_POSTGRES";

    private readonly PostgreSqlContainer? _container;
    private readonly NpgsqlConnectionStringBuilder? _external;

    public PostgresFixture()
    {
        var server = Environment.GetEnvironmentVariable(ExternalServerVariable);
        if (!string.IsNullOrWhiteSpace(server))
        {
            _external = new NpgsqlConnectionStringBuilder(server) { Database = $"fixture_{Guid.NewGuid():N}" };
            return;
        }

        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("test_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();
    }

    /// <summary>Gets the connection string of the test database.</summary>
    public string ConnectionString => _external?.ConnectionString ?? _container!.GetConnectionString();

    /// <summary>The disposable database this fixture owns (tests check it before destructive work).</summary>
    public string DatabaseName => _external?.Database ?? "test_db";

    /// <summary>Gets the database host.</summary>
    public string Host => _external?.Host ?? _container!.Hostname;

    /// <summary>Gets the database port.</summary>
    public int Port => _external?.Port ?? _container!.GetMappedPublicPort(5432);

    public async Task InitializeAsync()
    {
        if (_external is null)
        {
            await _container!.StartAsync();
            return;
        }

        await using var connection = new NpgsqlConnection(Maintenance(_external));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_external.Database}\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_external is null)
        {
            await _container!.StopAsync();
            await _container.DisposeAsync();
            return;
        }

        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(Maintenance(_external));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_external.Database}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string Maintenance(NpgsqlConnectionStringBuilder builder) =>
        new NpgsqlConnectionStringBuilder(builder.ConnectionString) { Database = "postgres", Pooling = false }.ConnectionString;
}
