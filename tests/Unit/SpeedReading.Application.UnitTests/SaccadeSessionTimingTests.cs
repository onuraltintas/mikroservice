using System.Reflection;
using System.Text.Json.Nodes;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class SaccadeSessionTimingTests
{
    [Fact]
    public async Task Saccade_requires_its_active_duration_and_never_invents_a_measured_score()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Saccade", "Sakkad", "motion_path");
        var exercise = Exercise.Create("Sakkad", "attention",
            """{"engineType":"motion_path","engineConfig":{"mode":"saccade","content":{"type":"number"},"timing":{"holdMs":800,"durationSeconds":5}}}""",
            1, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
        var implementation = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(implementation,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        var start = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteAsync(student, start.SessionId, new()));
        Assert.True((await service.ValidateActionAsync(student, start.SessionId, new() { Action = "tracking_start" })).IsValid);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteAsync(student, start.SessionId, new()));
        var session = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-6);
        session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        var result = await service.CompleteAsync(student, start.SessionId, new());
        Assert.Equal("NotMeasured", result.MeasurementStatus);
        Assert.Null(result.Score); Assert.Null(result.RawWPM); Assert.Null(result.WordsRead);
        await service.CompleteAsync(student, start.SessionId, new());
        Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
    }
}
