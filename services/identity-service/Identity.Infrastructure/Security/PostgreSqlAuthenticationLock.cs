using Identity.Application.Interfaces;
using Npgsql;

namespace Identity.Infrastructure.Security;

public sealed class PostgreSqlAuthenticationLock(NpgsqlDataSource dataSource) : IAuthenticationLock, IAsyncDisposable
{
    public ValueTask DisposeAsync() => dataSource.DisposeAsync();
    public async Task<IAsyncDisposable> AcquireAsync(Guid userId, CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var key = $"eduivme:authentication:{userId:D}";
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_advisory_lock(hashtextextended(@key, 0))";
            command.Parameters.AddWithValue("key", key);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new Lease(connection, key);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
    private sealed class Lease(NpgsqlConnection connection, string key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT pg_advisory_unlock(hashtextextended(@key, 0))";
                command.Parameters.AddWithValue("key", key);
                await command.ExecuteNonQueryAsync();
            }
            finally { await connection.DisposeAsync(); }
        }
    }
}
