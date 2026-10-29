using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Infrastructure.Privacy;

namespace Identity.API.IntegrationTests;

/// <summary>Review H4b: the retention queries translate on PostgreSQL.</summary>
[Collection("Database")]
public sealed class SpeedReadingDataRetentionPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Retention_runs_on_postgres()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var result = await new SpeedReadingDataRetentionProcessor(db, TimeProvider.System).RunAsync(CancellationToken.None);
            result.Should().Be(new SpeedReadingRetentionResult(0, 0, 0));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
