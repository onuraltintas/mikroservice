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
public sealed class SpeedReadingTrackingPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task PersistsOnlyCompletedObservationAndRejectsEarlyOrForeignCompletion()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid(); var type = ExerciseType.Create(Guid.NewGuid(), "EyeTracking", "Takip", "motion_path");
            var exercise = Exercise.Create("Takip", "motion_path", """{"mode":"tracking","timing":{"durationSeconds":5},"path":{"type":"circle"}}""", 1, student, type.Id);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
            var implementation = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(implementation, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var start = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteAsync(student, start.SessionId, new()));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CompleteAsync(Guid.NewGuid(), start.SessionId, new()));
            Assert.True((await service.ValidateActionAsync(student, start.SessionId, new() { Action = "tracking_start" })).IsValid);
            var session = await db.ExerciseSessions.SingleAsync(); var state = JsonNode.Parse(session.SessionDataJson)!;
            state["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-6); session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
            var result = await service.CompleteAsync(student, start.SessionId, new());
            Assert.Equal("NotMeasured", result.MeasurementStatus); Assert.Null(result.RawWPM); Assert.Null(result.WordsRead); Assert.Null(result.Score);
            await service.CompleteAsync(student, start.SessionId, new()); db.ChangeTracker.Clear();
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync()); Assert.Equal(0, await db.ReadingSessions.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
