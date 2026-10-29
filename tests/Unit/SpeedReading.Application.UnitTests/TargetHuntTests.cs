using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Decision 29.10.2026: the N-back attention exercise is replaced in programs by "Hedef Avı" — rows of letters or
/// short words in which the student taps every occurrence of one target among look-alike distractors before time
/// runs out. The server owns the layout, validates each tap and scores found targets minus wrong taps.
/// </summary>
public sealed class TargetHuntTests
{
    private const string Target = "b";

    [Fact]
    public void Settings_are_read_and_a_layout_has_exactly_the_configured_targets()
    {
        var settings = TargetHuntRules.Read(Config(rows: 6, columns: 8, targetCount: 12));

        var layout = TargetHuntRules.CreateLayout(settings, new Random(7));

        Assert.Equal(48, layout.Length);
        Assert.Equal(12, layout.Count(item => item == Target));
        Assert.All(layout.Where(item => item != Target), item => Assert.Contains(item, settings.Distractors));
        Assert.Equal(60, settings.TimeLimitSeconds);
    }

    [Theory]
    [InlineData("b", new[] { "d", "b" }, 3, 4, 3, 60)]   // target among distractors
    [InlineData("", new[] { "d" }, 3, 4, 3, 60)]         // no target
    [InlineData("b", new string[0], 3, 4, 3, 60)]        // no distractor
    [InlineData("b", new[] { "d" }, 3, 4, 7, 60)]        // more than half are targets
    [InlineData("b", new[] { "d" }, 2, 4, 2, 60)]        // too few rows
    [InlineData("b", new[] { "d" }, 3, 4, 3, 5)]         // time limit too short
    [InlineData("b c", new[] { "d" }, 3, 4, 3, 60)]      // whitespace in a token
    public void Inconsistent_settings_are_rejected(string target, string[] distractors, int rows, int columns, int targetCount, int timeLimit)
    {
        var json = JsonSerializer.Serialize(new { engineType = "target_hunt",
            engineConfig = new { target, distractors, rows, columns, targetCount, timeLimit } });

        Assert.ThrowsAny<ArgumentException>(() => ExerciseConfigurationRules.ValidateActiveConfiguration(json, "target_hunt"));
    }

    [Fact]
    public void The_engine_is_supported_classified_as_attention_and_estimated_by_its_time_limit()
    {
        Assert.Contains("target_hunt", ExerciseConfigurationRules.GetSupportedEngineTypes());
        Assert.Equal("target_hunt", ExerciseConfigurationRules.NormalizeEngineType("TargetHunt"));
        Assert.Equal("attention-and-eye-movement", ExerciseTaxonomy.ResolveCategoryKey("target_hunt"));
        ExerciseConfigurationRules.ValidateActiveConfiguration(ConfigJson(), "target_hunt");
        Assert.Equal(1, SpeedReadingDailyProgressRules.EstimateDurationMinutes(ConfigJson()));
        Assert.Equal(2, SpeedReadingDailyProgressRules.EstimateDurationMinutes(ConfigJson(timeLimit: 75)));
    }

    [Fact]
    public void Score_is_found_targets_minus_five_per_wrong_tap_capped_at_thirty()
    {
        Assert.Equal(100m, TargetHuntRules.Score(hits: 12, falseTaps: 0, targets: 12));
        Assert.Equal(45m, TargetHuntRules.Score(hits: 6, falseTaps: 1, targets: 12));
        Assert.Equal(70m, TargetHuntRules.Score(hits: 12, falseTaps: 9, targets: 12));
        Assert.Equal(0m, TargetHuntRules.Score(hits: 1, falseTaps: 9, targets: 12));
        Assert.Equal(25m, TargetHuntRules.Accuracy(hits: 1, falseTaps: 1, targets: 3));
    }

    [Fact]
    public async Task The_server_layout_is_given_to_the_student_and_taps_are_validated_against_it()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var id = await Seed(db, user);
        var service = Service(db);
        var started = await service.StartAsync(user, new() { ExerciseId = id });
        var items = Items(started.InitialData);
        Assert.Equal(12, items.Length);
        Assert.Equal(3, items.Count(item => item == Target));
        Assert.Equal(3, started.TotalSteps);

        Assert.False((await service.ValidateActionAsync(user, started.SessionId, new() { Action = "target_hunt_tap", Index = 0 })).IsValid);
        Assert.True((await service.ValidateActionAsync(user, started.SessionId, new() { Action = "target_hunt_start" })).IsValid);
        var wrongIndex = Array.FindIndex(items, item => item != Target);
        var targetIndex = Array.IndexOf(items, Target);
        var wrong = await service.ValidateActionAsync(user, started.SessionId, new() { Action = "target_hunt_tap", Index = wrongIndex });
        Assert.True(wrong.IsValid); Assert.False(wrong.IsCorrect);
        Assert.False((await service.ValidateActionAsync(user, started.SessionId, new() { Action = "target_hunt_tap", Index = wrongIndex })).IsValid);
        Assert.False((await service.ValidateActionAsync(user, started.SessionId, new() { Action = "target_hunt_tap", Index = 99 })).IsValid);
        Assert.True((await service.ValidateActionAsync(user, started.SessionId, new() { Action = "target_hunt_tap", Index = targetIndex })).IsCorrect);
        await Assert.ThrowsAnyAsync<Exception>(() => service.CompleteAsync(user, started.SessionId, new()));
        Assert.True((await service.ValidateActionAsync(user, started.SessionId, new() { Action = "target_hunt_finish" })).IsCompleted);

        var result = await service.CompleteAsync(user, started.SessionId, new() { CustomData = new() {
            ["score"] = JsonSerializer.SerializeToElement(100) } });

        Assert.Equal(1, result.CorrectCount); Assert.Equal(1, result.IncorrectCount);
        Assert.Equal(25m, result.Accuracy); Assert.Equal(28.33m, result.Score); Assert.Null(result.RawWPM);
        Assert.Equal(1, result.DetailedResults.GetProperty("targetHuntHits").GetArrayLength());
        Assert.Equal(1, result.DetailedResults.GetProperty("targetHuntFalseTaps").GetArrayLength());
    }

    [Fact]
    public async Task Finding_every_target_finishes_the_round_with_full_score()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var id = await Seed(db, user);
        var service = Service(db);
        var started = await service.StartAsync(user, new() { ExerciseId = id });
        var items = Items(started.InitialData);
        await service.ValidateActionAsync(user, started.SessionId, new() { Action = "target_hunt_start" });
        ExerciseActionValidationResponse? last = null;
        foreach (var index in Enumerable.Range(0, items.Length).Where(index => items[index] == Target))
            last = await service.ValidateActionAsync(user, started.SessionId, new() { Action = "target_hunt_tap", Index = index });

        Assert.True(last!.IsCompleted);
        var result = await service.CompleteAsync(user, started.SessionId, new());
        Assert.Equal(100m, result.Score); Assert.Equal(3, result.CorrectCount);
        Assert.True(result.XpGained > 0);
    }

    [Fact]
    public async Task The_server_clock_ends_the_round_without_accepting_a_late_tap()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var id = await Seed(db, user);
        var service = Service(db);
        var started = await service.StartAsync(user, new() { ExerciseId = id });
        var targetIndex = Array.IndexOf(Items(started.InitialData), Target);
        await service.ValidateActionAsync(user, started.SessionId, new() { Action = "target_hunt_start" });
        var session = await db.ExerciseSessions.SingleAsync();
        var state = System.Text.Json.Nodes.JsonNode.Parse(session.SessionDataJson)!;
        state["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-100);
        session.SetState(state.ToJsonString()); await db.SaveChangesAsync();

        var late = await service.ValidateActionAsync(user, started.SessionId, new() { Action = "target_hunt_tap", Index = targetIndex });

        Assert.True(late.IsCompleted); Assert.Null(late.IsCorrect);
        var result = await service.CompleteAsync(user, started.SessionId, new());
        Assert.Equal(60, result.TimeSpentSeconds); Assert.Equal(0m, result.Score); Assert.Equal(0, result.CorrectCount);
    }

    private static string[] Items(JsonElement initialData) =>
        initialData.GetProperty("targetHuntItems").EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static JsonElement Config(int rows = 3, int columns = 4, int targetCount = 3, int timeLimit = 60) =>
        JsonSerializer.SerializeToElement(new { target = Target, distractors = new[] { "d", "p" }, rows, columns, targetCount, timeLimit });

    private static string ConfigJson(int timeLimit = 60) => JsonSerializer.Serialize(new { engineType = "target_hunt",
        engineConfig = new { target = Target, distractors = new[] { "d", "p" }, rows = 3, columns = 4, targetCount = 3, timeLimit } });

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ISpeedReadingExerciseSessions Service(OwnedSpeedReadingDbContext db) => (ISpeedReadingExerciseSessions)Activator.CreateInstance(
        typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;

    private static async Task<Guid> Seed(OwnedSpeedReadingDbContext db, Guid user)
    {
        var type = ExerciseType.Create(Guid.NewGuid(), "TargetHunt", "Hedef Avı", "target_hunt");
        var exercise = Exercise.Create("Hedef Avı", "target_hunt", ConfigJson(), 1, user, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
        return exercise.Id;
    }
}
