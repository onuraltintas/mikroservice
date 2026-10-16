using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.ProgramProgression.IntegrationTests;

public sealed class VocabularyScoringPostgresTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private OwnedSpeedReadingDbContext Open() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseNpgsql(postgres.ConnectionString, b => b.MigrationsHistoryTable("__ef_migrations_history", "speed_reading")).Options);

    [Theory]
    [InlineData("quiz", false)]
    [InlineData("quiz", true)]
    [InlineData("learning", false)]
    [InlineData("learning", true)]
    public async Task Vocabulary_completion_preserves_points_and_is_idempotent_across_requests(string mode, bool existingPoints)
    {
        await using var db = Open();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        await VocabularyScoringScenario.Run(Open, mode, existingPoints);
    }
}
