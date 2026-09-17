using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// Serializes automatic and manual program assignment for one student.
/// PostgreSQL advisory locks are transaction-scoped, so the lock is released
/// automatically when the returned transaction is committed or disposed.
/// </summary>
internal static class OwnedSpeedReadingProgramAssignmentLock
{
    public static async Task<T> ExecuteAsync<T>(
        OwnedSpeedReadingDbContext db,
        Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!IsPostgres(db))
            return await operation();

        var executionStrategy = db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(operation);
    }

    public static async Task ExecuteAsync(
        OwnedSpeedReadingDbContext db,
        Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!IsPostgres(db))
        {
            await operation();
            return;
        }

        var executionStrategy = db.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(operation);
    }

    public static async Task<IDbContextTransaction?> AcquireAsync(
        OwnedSpeedReadingDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!IsPostgres(db))
            return null;

        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({GetLockKey(userId)})",
                cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private static bool IsPostgres(OwnedSpeedReadingDbContext db) =>
        db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    private static long GetLockKey(Guid userId)
    {
        var bytes = userId.ToByteArray();
        return BitConverter.ToInt64(bytes, 0) ^ BitConverter.ToInt64(bytes, 8);
    }
}
