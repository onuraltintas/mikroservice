using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Review H1 (finding 24): a date from the query string ("?dateFrom=2026-10-01") binds with DateTimeKind.Unspecified.
/// Npgsql refuses to compare it with a timestamptz column, so history and notification lists answered 500.
/// </summary>
[Collection("Database")]
public sealed class SpeedReadingQueryDatePostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task History_and_notification_lists_accept_dates_without_a_time_zone()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var user = Guid.NewGuid();
            DateTime? from = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified);
            DateTime? to = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Unspecified);

            await Call("Persistence.OwnedSpeedReadingProgress", [db], "GetReadingHistoryAsync", user, null, from, to, CancellationToken.None);
            await Call("Persistence.OwnedSpeedReadingStudentReading", [db], "GetHistoryAsync", user, null, from, to, null, CancellationToken.None);
            await Call("Persistence.OwnedSpeedReadingPrograms", [db, null, null], "GetDailyExerciseLogsAsync", user, from, to, 10, CancellationToken.None);
            await Call("Legacy.LegacySpeedReadingNotifications", [db, null], "GetNotificationsAsync", user, null, null, from, to, 1, 20, CancellationToken.None);
            await Call("Legacy.LegacySpeedReadingNotifications", [db, null], "GetAllAsync", null, null, null, null, from, to, null, 1, 20, CancellationToken.None);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static async Task Call(string typeName, object?[] constructorArguments, string method, params object?[] arguments)
    {
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType($"SpeedReading.Infrastructure.{typeName}")!;
        var instance = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, constructorArguments, null)!;
        var target = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(item => item.Name == method && item.GetParameters().Length == arguments.Length);
        await (Task)target.Invoke(instance, arguments)!;
    }
}
