using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Round counts come from the catalog when a session starts and are then owned by the session:
/// a later catalog change (such as the November 2026 round-count migration) applies to new sessions only.
/// </summary>
public sealed class RoundCountSessionTests
{
    private const string OldTachistoscope = """{"engineType":"text_stream","engineConfig":{"mode":"tachistoscope","content":{"type":"number","count":20,"length":3},"timing":{"durationMs":1500,"intervalMs":750},"adaptive":{"enabled":true,"minLength":3,"maxLength":5,"minDurationMs":400,"maxDurationMs":2000}},"difficultyLevel":1}""";
    private const string NewTachistoscope = """{"engineType":"text_stream","engineConfig":{"mode":"tachistoscope","content":{"type":"number","count":15,"length":3},"timing":{"durationMs":1500,"intervalMs":750},"adaptive":{"enabled":true,"minLength":3,"maxLength":5,"minDurationMs":400,"maxDurationMs":2000}},"difficultyLevel":1}""";
    private const string OldVisualRoot = """{"rounds":30,"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","displayDurationMs":1200,"startDegrees":8,"targetDegrees":16,"content":{"stimulusType":"letter"}},"difficultyLevel":1}""";
    private const string NewVisualRoot = """{"rounds":15,"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","displayDurationMs":1200,"startDegrees":8,"targetDegrees":16,"content":{"stimulusType":"letter"}},"difficultyLevel":1}""";
    private const string OldVisualNested = """{"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","rounds":30,"displayDurationMs":1200,"startDegrees":8,"targetDegrees":16,"content":{"stimulusType":"letter"}},"difficultyLevel":1}""";
    private const string NewVisualNested = """{"engineType":"visual_expansion","engineConfig":{"mode":"horizontal","rounds":15,"displayDurationMs":1200,"startDegrees":8,"targetDegrees":16,"content":{"stimulusType":"letter"}},"difficultyLevel":1}""";

    [Theory]
    [InlineData("Tachistoscope", "text_stream", OldTachistoscope, NewTachistoscope, 20, 15)]
    [InlineData("VisualExpansion", "visual_expansion", OldVisualRoot, NewVisualRoot, 30, 15)]
    [InlineData("VisualExpansion", "visual_expansion", OldVisualNested, NewVisualNested, 30, 15)]
    public async Task New_sessions_use_the_updated_round_count_and_open_sessions_keep_their_own(
        string typeName, string engineType, string oldConfiguration, string newConfiguration, int oldRounds, int newRounds)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var actor = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), typeName, typeName, engineType);
        var exercise = Exercise.Create("Alıştırma", typeName, oldConfiguration, 1, actor, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
        await db.SaveChangesAsync();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;

        var student = Guid.NewGuid();
        var open = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        Assert.Equal(oldRounds, open.TotalSteps);

        exercise.Update(exercise.Title, null, typeName, newConfiguration, 1, type.Id, null, actor, DateTime.UtcNow);
        await db.SaveChangesAsync();

        // Resuming the open session keeps its step count, and the player receives that count
        // (the catalog configuration in the same response already carries the new value).
        var resumed = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        Assert.Equal(open.SessionId, resumed.SessionId);
        Assert.Equal(oldRounds, resumed.TotalSteps);
        if (typeName == "Tachistoscope")
            Assert.Equal(oldRounds, resumed.InitialData.GetProperty("tachistoscope").GetProperty("count").GetInt32());
        else
            Assert.Equal(oldRounds, resumed.InitialData.GetProperty("rounds").GetInt32());

        var fresh = await service.StartAsync(Guid.NewGuid(), new() { ExerciseId = exercise.Id });
        Assert.Equal(newRounds, fresh.TotalSteps);
        if (typeName == "Tachistoscope")
            Assert.Equal(newRounds, fresh.InitialData.GetProperty("tachistoscope").GetProperty("count").GetInt32());
        else
            Assert.Equal(newRounds, fresh.InitialData.GetProperty("rounds").GetInt32());
    }
}
