using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
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
            var type = ExerciseType.Create(Guid.NewGuid(), "RSVP", "RSVP", "text_stream");
            var exercise = Exercise.Create("Okuma", "rsvp", "{}", 1, Guid.NewGuid(), type.Id);
            db.ExerciseTypes.Add(type);
            db.Exercises.Add(exercise);
            var ended = DateTime.UtcNow.AddDays(-120);
            var session = ExerciseSession.Import(Guid.NewGuid(), Guid.NewGuid(), exercise.Id, null, null, ExerciseSessionStatus.Completed, ended.AddMinutes(-5), ended,
                0, null, null, 1, 1, 1, 0, "{\"comprehensionMode\":\"learning\",\"text\":\"uzun metin\"}", null, "{\"a\":{}}", ended, null, null, null);
            db.ExerciseSessions.Add(session);
            await db.SaveChangesAsync();

            var first = await new SpeedReadingDataRetentionProcessor(db, TimeProvider.System).RunAsync(CancellationToken.None);
            var second = await new SpeedReadingDataRetentionProcessor(db, TimeProvider.System).RunAsync(CancellationToken.None);

            first.CompactedSessions.Should().Be(1);
            second.CompactedSessions.Should().Be(0);
            db.ChangeTracker.Clear();
            var stored = await db.ExerciseSessions.SingleAsync();
            stored.SessionDataJson.Should().Contain("learning").And.NotContain("uzun metin");
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
