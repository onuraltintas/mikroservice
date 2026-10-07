using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingTextFadePostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Persists_verified_full_and_partial_fade_without_fabricated_metrics(bool partial)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // PostgresFixture owns a disposable Testcontainers database, never a live database.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "TextFading", "Metin Solma", "text_fade");
            var config = new JsonObject {
                ["engineType"] = "text_fade", ["engineConfig"] = new JsonObject {
                    ["fading"] = new JsonObject { ["speedWpm"] = 200, ["lagMs"] = 300 },
                    ["timing"] = new JsonObject { ["timeLimitSec"] = partial ? 1 : 0 }
                }
            };
            var exercise = Exercise.Create("Metin Solma", "reading", config.ToJsonString(), 3, student, type.Id);
            db.ExerciseTypes.Add(type);
            db.Exercises.Add(exercise);
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Yanlış", "yanlış seviye", difficultyLevel: 1));
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "A", "bir iki üç dört", difficultyLevel: 3));
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "B", "beş altı yedi sekiz", difficultyLevel: 3));
            await db.SaveChangesAsync();
            var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.Equal(1500, started.InitialData.GetProperty("readingMinimumMs").GetInt32());
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId, new() { Action = "start_reading" }));
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "start_reading" })).IsValid);
            await service.PauseAsync(student, started.SessionId);
            await service.ResumeAsync(student, started.SessionId);
            var session = await db.ExerciseSessions.SingleAsync();
            var textId = session.ReadingTextId;
            var state = JsonNode.Parse(session.SessionDataJson)!;
            state["readingStartTime"] = DateTime.UtcNow.AddMilliseconds(partial ? -1100 : -1600);
            session.SetState(state.ToJsonString());
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            // Even a client claiming full completion cannot bypass the server deadline.
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" })).IsValid);
            var result = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(result.RawWPM);
            Assert.Null(result.Accuracy);
            Assert.Null(result.ComprehensionScore);
            Assert.Equal(0, result.XpGained);
            Assert.Null(result.WordsRead);
            Assert.Equal(partial, result.DetailedResults.GetProperty("readingIncomplete").GetBoolean());
            Assert.Equal(200, result.DetailedResults.GetProperty("fadeDisplayPaceWpm").GetDecimal());
            Assert.Equal(partial ? 50 : 100, result.DetailedResults.GetProperty("fadeCompletionPercent").GetDecimal());
            db.ChangeTracker.Clear();
            var repeat = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(repeat.RawWPM);
            Assert.Equal(result.DetailedResults.GetProperty("fadeCompletionPercent").GetDecimal(), repeat.DetailedResults.GetProperty("fadeCompletionPercent").GetDecimal());
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
            await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.NotEqual(textId, (await db.ExerciseSessions.SingleAsync(item => item.Id != started.SessionId)).ReadingTextId);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
