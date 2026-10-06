using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.Progress;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class FocusPersistenceTests
{
    [Fact]
    public async Task Pause_before_focus_start_does_not_reduce_exercise_active_time()
    {
        await using var db = Context(); var (student, exercise) = await Seed(db);
        var service = Service<ISpeedReadingExerciseSessions>(db, "OwnedSpeedReadingExerciseSessions");
        var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        await service.PauseAsync(student, started.SessionId);
        var data = JsonNode.Parse((await db.ExerciseSessions.SingleAsync()).SessionDataJson)!;
        Assert.Null(data["focusPausedAt"]);
        await service.ResumeAsync(student, started.SessionId);
        await service.ValidateActionAsync(student, started.SessionId, new() { Action = "focus_start" });
        await Elapse(db, 3);
        Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "complete" })).IsValid);
    }
    [Fact]
    public async Task Short_pauses_are_accumulated_in_milliseconds_and_do_not_allow_early_completion()
    {
        await using var db = Context(); var (student, exercise) = await Seed(db);
        var service = Service<ISpeedReadingExerciseSessions>(db, "OwnedSpeedReadingExerciseSessions");
        var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        await service.ValidateActionAsync(student, started.SessionId, new() { Action = "focus_start" });
        await Elapse(db, 4);
        for (var index = 0; index < 4; index++) {
            await service.PauseAsync(student, started.SessionId);
            var session = await db.ExerciseSessions.SingleAsync(); var state = JsonNode.Parse(session.SessionDataJson)!;
            state["focusPausedAt"] = DateTime.UtcNow.AddMilliseconds(-400);
            session.SetState(state.ToJsonString(), session.CustomDataJson);
            typeof(ExerciseSession).GetProperty(nameof(ExerciseSession.PausedAt))!.SetValue(session, DateTime.UtcNow.AddMilliseconds(-400));
            await db.SaveChangesAsync(); await service.ResumeAsync(student, started.SessionId);
        }
        Assert.False((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "complete" })).IsValid);
        var data = JsonNode.Parse((await db.ExerciseSessions.SingleAsync()).SessionDataJson)!;
        Assert.True(data["focusPausedMilliseconds"]!.GetValue<long>() >= 1600);
    }

    [Theory]
    [InlineData("[0]")]
    [InlineData("[99]")]
    [InlineData("[1]")]
    public async Task Contradictory_focus_target_hints_cannot_start(string targets)
    {
        await using var db = Context(); var (student, exercise) = await Seed(db, config:
            $$$"""{"engineType":"focus","totalSteps":3,"positionSequence":[1,2,1],"positionTargetIndices":{{{targets}}}}""");
        var service = Service<ISpeedReadingExerciseSessions>(db, "OwnedSpeedReadingExerciseSessions");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(student, new() { ExerciseId = exercise }));
    }

    private static T Service<T>(OwnedSpeedReadingDbContext db, string name) => (T)Activator.CreateInstance(
        typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence." + name)!,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(Guid student, Guid exercise)> Seed(OwnedSpeedReadingDbContext db, string mode = "position", string? config = null)
    {
        var student = Guid.NewGuid(); var type = ExerciseType.Create(Guid.NewGuid(), "Focus", "Odaklanma", "focus");
        var exercise = Exercise.Create("Odaklanma", "focus", config ?? $$$"""{"engineType":"focus","totalSteps":3,"engineConfig":{"mode":"{{{mode}}}","nLevel":1,"speedMs":1000,"gridSize":3,"positionSequence":[1,2,1],"wordSequence":["a","b","a"]}}""", 3, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync(); return (student, exercise.Id);
    }

    private static async Task Elapse(OwnedSpeedReadingDbContext db, int seconds)
    {
        var session = await db.ExerciseSessions.SingleAsync();
        var json = JsonNode.Parse(session.SessionDataJson)!;
        json["focusStartTime"] = DateTime.UtcNow.AddSeconds(-seconds);
        json["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-seconds);
        session.SetState(json.ToJsonString(), session.CustomDataJson);
        typeof(ExerciseSession).GetProperty(nameof(ExerciseSession.StartTime))!.SetValue(session, DateTime.UtcNow.AddSeconds(-seconds));
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData("position")]
    [InlineData("word")]
    [InlineData("dual")]
    public async Task Unanswered_trials_complete_only_after_full_duration_and_without_reading_metrics(string mode)
    {
        await using var db = Context(); var (student, exercise) = await Seed(db, mode);
        var service = Service<ISpeedReadingExerciseSessions>(db, "OwnedSpeedReadingExerciseSessions");
        var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        await service.ValidateActionAsync(student, started.SessionId, new() { Action = "focus_start" });
        Assert.False((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "complete" })).IsValid);
        await Elapse(db, 10);
        Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "complete" })).IsValid);
        var result = await service.CompleteAsync(student, started.SessionId, new());
        Assert.Equal(0m, result.Accuracy); Assert.Null(result.RawWPM); Assert.Null(result.WordsRead);
        var repeated = await service.CompleteAsync(student, started.SessionId, new());
        Assert.Null(repeated.RawWPM); Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
    }

    [Theory]
    [InlineData("{\"engineType\":\"focus\",\"mode\":\"invalid\"}")]
    [InlineData("{\"engineType\":\"focus\",\"gridSize\":3,\"positionSequence\":[1,10,1]}")]
    [InlineData("{\"engineType\":\"focus\",\"mode\":\"dual\",\"positionSequence\":[1,2,1],\"wordSequence\":[\"a\"]}")]
    public async Task Invalid_focus_content_cannot_start(string configuration)
    {
        await using var db = Context(); var (student, exercise) = await Seed(db, config: configuration);
        var service = Service<ISpeedReadingExerciseSessions>(db, "OwnedSpeedReadingExerciseSessions");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(student, new() { ExerciseId = exercise }));
        Assert.Equal(0, await db.ExerciseSessions.CountAsync());
    }

    [Fact]
    public async Task Legacy_focus_metrics_are_hidden_on_replay_and_progress_summary()
    {
        await using var db = Context(); var (student, exercise) = await Seed(db);
        var service = Service<ISpeedReadingExerciseSessions>(db, "OwnedSpeedReadingExerciseSessions");
        var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        db.ExerciseSessionResults.Add(SpeedReading.Domain.Sessions.ExerciseSessionResult.Create(Guid.NewGuid(), started.SessionId, student, exercise, null, 50, 10, 300, 100, 300, 100, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var replay = await service.CompleteAsync(student, started.SessionId, new());
        Assert.Null(replay.RawWPM); Assert.Null(replay.WordsRead); Assert.Null(replay.WeightedKDP);
        var writer = Service<ISpeedReadingProgressWriter>(db, "OwnedSpeedReadingProgressWriter");
        var summary = await writer.CreateExerciseResultAsync(student, new(exercise, null, 0, 0, 0, 0, 0, null, null, SessionId: started.SessionId), "focus-legacy-summary-key");
        Assert.Null(summary.RawWpm); Assert.Equal(0, summary.WordsRead); Assert.Null(summary.WeightedKdp);
    }
}
