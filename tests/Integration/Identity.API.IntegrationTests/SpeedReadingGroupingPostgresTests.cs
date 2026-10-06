using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingGroupingPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Persists_complete_and_partial_grouping_without_fabricated_reading_metrics(bool partial)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // This fixture owns an isolated disposable Testcontainers database.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Chunking", "Gruplama", "word_highlight");
            var exercise = Exercise.Create("Gruplama", "reading",
                """{"engineType":"word_highlight","engineConfig":{"mode":"chunking","pacer":{"chunkSize":2,"speedWpm":200}}}""",
                3, student, type.Id);
            db.ExerciseTypes.Add(type);
            db.Exercises.Add(exercise);
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Yanlış", "yanlış seviye", difficultyLevel: 1));
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "A", "bir iki üç dört", difficultyLevel: 3));
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "B", "beş altı yedi sekiz", difficultyLevel: 3));
            await db.SaveChangesAsync();
            var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId, new() { Action = "start_reading" }));
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "start_reading" })).IsValid);
            await service.PauseAsync(student, started.SessionId);
            await service.ResumeAsync(student, started.SessionId);
            var session = await db.ExerciseSessions.SingleAsync();
            var textId = session.ReadingTextId;
            var state = JsonNode.Parse(session.SessionDataJson)!;
            state["readingStartTime"] = DateTime.UtcNow.AddMilliseconds(partial ? -600 : -2000);
            session.SetState(state.ToJsonString());
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading", IsTimeout = partial })).IsValid);
            var result = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(result.RawWPM);
            Assert.Null(result.Accuracy);
            Assert.Null(result.ComprehensionScore);
            Assert.Equal(0, result.XpGained);
            Assert.Equal(partial, result.DetailedResults.GetProperty("readingIncomplete").GetBoolean());
            Assert.Equal(200, result.DetailedResults.GetProperty("groupingDisplayPaceWpm").GetDecimal());
            var completion = result.DetailedResults.GetProperty("groupingCompletionPercent").GetDecimal();
            Assert.InRange(completion, partial ? 40 : 100, partial ? 80 : 100);
            db.ChangeTracker.Clear();
            var repeat = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(repeat.RawWPM);
            Assert.Equal(completion, repeat.DetailedResults.GetProperty("groupingCompletionPercent").GetDecimal());
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
            await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.NotEqual(textId, (await db.ExerciseSessions.SingleAsync(item => item.Id != started.SessionId)).ReadingTextId);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
