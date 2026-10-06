using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingFreeReadingPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Free_reading_persists_owned_precise_result_once_and_rotates_content()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try {
            await db.Database.EnsureCreatedAsync(); var user = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "FreeReading", "Serbest Okuma", "free_reading");
            var exercise = Exercise.Create("Serbest Okuma", "free_reading", "{\"engineType\":\"free_reading\",\"engineConfig\":{\"content\":{\"minWordCount\":50},\"timing\":{\"minReadingTimeMs\":3000,\"maxReadingTimeMs\":60000}}}", 2, user, type.Id);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
            for (var i = 0; i < 2; i++) db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Metin", string.Join(" ", Enumerable.Repeat("kelime", 100)), difficultyLevel: 2, exerciseId: exercise.Id));
            await db.SaveChangesAsync();
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(user, new() { ExerciseId = exercise.Id });
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CompleteAsync(Guid.NewGuid(), started.SessionId, new()));
            await service.ValidateActionAsync(user, started.SessionId, new() { Action = "start_reading" });
            Assert.False((await service.ValidateActionAsync(user, started.SessionId, new() { Action = "finish_reading" })).IsValid);
            var session = await db.ExerciseSessions.SingleAsync(); var firstText = session.ReadingTextId;
            var state = JsonNode.Parse(session.SessionDataJson)!; var end = DateTime.UtcNow.AddSeconds(-2);
            state["readingStartTime"] = end.AddMilliseconds(-30500); state["readingEndTime"] = end;
            state["readingPausedMilliseconds"] = 250; session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
            var result = await service.CompleteAsync(user, started.SessionId, new());
            Assert.Equal(198.35m, result.RawWPM); Assert.Null(result.ComprehensionScore);
            Assert.Equal(result.RawWPM, (await service.CompleteAsync(user, started.SessionId, new())).RawWPM);
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync()); Assert.Equal(1, await db.ReadingSessions.CountAsync());
            var next = await service.StartAsync(user, new() { ExerciseId = exercise.Id });
            Assert.NotEqual(firstText, (await db.ExerciseSessions.SingleAsync(s => s.Id == next.SessionId)).ReadingTextId);
        } finally { await db.Database.EnsureDeletedAsync(); }
    }
}
