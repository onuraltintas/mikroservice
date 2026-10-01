using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingSchemaMigrationTests(PostgresFixture postgresFixture)
{
    [Fact]
    public async Task Migrate_should_create_subscription_plan_table_in_postgres()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgresFixture.ConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "coaching"))
            .Options;

        await using var context = new CoachingDbContext(options);
        await context.Database.EnsureDeletedAsync();

        try
        {
            await context.Database.MigrateAsync();

            await using var connection = new NpgsqlConnection(postgresFixture.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT to_regclass('coaching.subscription_plans') IS NOT NULL",
                connection);

            Assert.True((bool?)await command.ExecuteScalarAsync());
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }
}

[Collection("Database")]
public sealed class SpeedReadingSchemaMigrationTests(PostgresFixture postgresFixture)
{
    [Fact]
    public async Task Migrate_should_create_owned_user_profile_table_in_postgres()
    {
        var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgresFixture.ConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "speed_reading"))
            .Options;

        await using var context = new OwnedSpeedReadingDbContext(options);
        await context.Database.EnsureDeletedAsync();

        try
        {
            await context.Database.MigrateAsync();

            await using var connection = new NpgsqlConnection(postgresFixture.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT to_regclass('speed_reading.user_profiles') IS NOT NULL",
                connection);

            Assert.True((bool?)await command.ExecuteScalarAsync());
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }
}
