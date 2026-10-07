using System.Reflection;
using System.Text.Json.Nodes;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class TrackingSessionTests
{
    [Fact]
    public async Task RejectsEarlyCompletionThenPersistsAnUnmeasuredTimedSession()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "EyeTracking", "Takip", "motion_path");
        var exercise = Exercise.Create("Takip", "motion_path", """{"mode":"tracking","timing":{"durationSeconds":5},"path":{"type":"circle"}}""", 1, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
        var implementation = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(implementation,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        var start = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteAsync(student, start.SessionId, new()));
        await service.ValidateActionAsync(student, start.SessionId, new() { Action = "start" });
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteAsync(student, start.SessionId, new()));
        var session = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-6);
        session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        var result = await service.CompleteAsync(student, start.SessionId, new());
        Assert.Equal("NotMeasured", result.MeasurementStatus);
        Assert.Null(result.RawWPM); Assert.Null(result.WordsRead); Assert.Null(result.WeightedKDP);
        await service.CompleteAsync(student, start.SessionId, new());
        Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
    }
}
