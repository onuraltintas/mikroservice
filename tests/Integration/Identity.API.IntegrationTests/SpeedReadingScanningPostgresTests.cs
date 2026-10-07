using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingScanningPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scanning_snapshot_and_results_survive_reload_without_client_scores(bool timeout)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // Only the isolated disposable Testcontainers database is reset.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Scanning", "Tarama", "scanning");
            var exercise = Exercise.Create("Tarama", "reading", """{"engineConfig":{"targetCount":2,"timeLimit":10}}""", 3, student, type.Id);
            db.ExerciseTypes.Add(type);
            db.Exercises.Add(exercise);
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Wrong", "başka seviye", difficultyLevel: 1));
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Text", "ışık inci", difficultyLevel: 3));
            await db.SaveChangesAsync();
            var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId, new() { Action = "scan_start" }));
            await service.ValidateActionAsync(student, started.SessionId, new() { Action = "scan_start" });
            var click = new ExerciseActionRequest { ActionId = Guid.NewGuid(), Action = "scan_click", Index = 0, Number = 0 };
            await service.ValidateActionAsync(student, started.SessionId, click);
            db.ChangeTracker.Clear();
            await service.ValidateActionAsync(student, started.SessionId, click);
            var session = await db.ExerciseSessions.SingleAsync();
            Assert.Equal(1, session.CorrectCount);
            if (timeout)
            {
                var state = JsonNode.Parse(session.SessionDataJson)!;
                state["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-20);
                session.SetState(state.ToJsonString());
                await db.SaveChangesAsync();
                Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "scan_timeout" })).IsCompleted);
            }
            else await service.ValidateActionAsync(student, started.SessionId, new() { Action = "scan_click", Index = 1, Number = 0 });
            db.ChangeTracker.Clear();
            var result = await service.CompleteAsync(student, started.SessionId, new() {
                CustomData = new Dictionary<string, JsonElement> { ["score"] = JsonSerializer.SerializeToElement(99999) }
            });
            Assert.Equal(timeout ? 50m : 100m, result.Accuracy);
            Assert.Null(result.RawWPM);
            Assert.Null(result.WordsRead);
            if (timeout) Assert.Equal(0, result.XpGained);
            db.ChangeTracker.Clear();
            var repeated = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Equal(result.Accuracy, repeated.Accuracy);
            Assert.Empty(await db.ReadingSessions.ToListAsync());
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
