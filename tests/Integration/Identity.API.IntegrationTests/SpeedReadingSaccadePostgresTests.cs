using System.Reflection;
using System.Text.Json.Nodes;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingSaccadePostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Requires_active_duration_and_saves_only_an_owned_unmeasured_completion_once()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Saccade", "Sakkad", "motion_path");
            var exercise = Exercise.Create("Sakkad", "attention",
                """{"engineType":"motion_path","engineConfig":{"mode":"saccade","timing":{"holdMs":800,"durationSeconds":5},"content":{"type":"number"}}}""",
                1, student, type.Id);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
            var implementation = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(implementation,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var start = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CompleteAsync(Guid.NewGuid(), start.SessionId, new()));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteAsync(student, start.SessionId, new()));
            Assert.True((await service.ValidateActionAsync(student, start.SessionId, new() { Action = "tracking_start" })).IsValid);
            var session = await db.ExerciseSessions.SingleAsync();
            var state = JsonNode.Parse(session.SessionDataJson)!;
            state["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-6);
            session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
            await service.PauseAsync(student, start.SessionId);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteAsync(student, start.SessionId, new()));
            // Test that a pause cannot be used to satisfy active duration, without sleeping.
            state = JsonNode.Parse(session.SessionDataJson)!;
            state["timingStartedAt"] = DateTime.UtcNow;
            session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
            await service.ResumeAsync(student, start.SessionId);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteAsync(student, start.SessionId, new()));
            state = JsonNode.Parse(session.SessionDataJson)!;
            state["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-6);
            session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
            var result = await service.CompleteAsync(student, start.SessionId, new());
            Assert.Equal("NotMeasured", result.MeasurementStatus);
            Assert.Null(result.Score); Assert.Null(result.RawWPM); Assert.Null(result.WordsRead);
            await service.CompleteAsync(student, start.SessionId, new());
            db.ChangeTracker.Clear();
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
            Assert.Equal(0, await db.ReadingSessions.CountAsync());
        } finally { await db.Database.EnsureDeletedAsync(); }
    }
}
