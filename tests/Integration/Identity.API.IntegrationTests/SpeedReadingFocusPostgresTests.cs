using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingFocusPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("position")]
    [InlineData("word")]
    [InlineData("dual")]
    public async Task Focus_results_are_owned_timed_idempotent_and_never_reading_speed(string mode)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // Isolated Testcontainers database only.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync(); var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Focus", "Odaklanma", "focus");
            var exercise = Exercise.Create("Odaklanma", "focus",
                $$$"""{"engineType":"focus","totalSteps":3,"engineConfig":{"mode":"{{{mode}}}","nLevel":1,"speedMs":1000,"positionSequence":[1,2,1],"wordSequence":["a","b","a"]}}""", 3, student, type.Id);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
                .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.DoesNotContain("targetIndices", started.InitialData.GetRawText(), StringComparison.OrdinalIgnoreCase);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CompleteAsync(Guid.NewGuid(), started.SessionId, new()));
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "focus_start" })).IsValid);
            Assert.False((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "complete" })).IsValid);
            var session = await db.ExerciseSessions.SingleAsync(); var state = JsonNode.Parse(session.SessionDataJson)!;
            state["focusStartTime"] = DateTime.UtcNow.AddSeconds(-10);
            state["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-10);
            session.SetState(state.ToJsonString(), session.CustomDataJson);
            typeof(ExerciseSession).GetProperty(nameof(ExerciseSession.StartTime))!.SetValue(session, DateTime.UtcNow.AddSeconds(-10));
            await db.SaveChangesAsync();
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "complete" })).IsValid);
            var result = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(result.RawWPM); Assert.Null(result.WordsRead); Assert.Equal(0m, result.Accuracy);
            db.ChangeTracker.Clear();
            var repeated = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(repeated.RawWPM); Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
            Assert.Equal(0m, (await db.ExerciseSessionResults.SingleAsync()).RawWpm);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
