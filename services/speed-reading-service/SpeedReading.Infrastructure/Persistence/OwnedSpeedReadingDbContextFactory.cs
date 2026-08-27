using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingDbContextFactory
    : IDesignTimeDbContextFactory<OwnedSpeedReadingDbContext>
{
    public OwnedSpeedReadingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SPEED_READING_OWNED_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=speedreading_owned_db;Username=eduplatform;Password=dev-only";

        var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "speed_reading"))
            .Options;

        return new OwnedSpeedReadingDbContext(options);
    }
}
