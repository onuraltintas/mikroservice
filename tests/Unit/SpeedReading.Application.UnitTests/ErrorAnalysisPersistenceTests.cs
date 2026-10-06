using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class ErrorAnalysisPersistenceTests
{
    [Fact]
    public async Task Unsupported_or_duplicate_selections_cannot_invent_progress_and_hints_are_idempotent()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var id = await Seed(db, user);
        var service = Service(db); var start = await service.StartAsync(user, new() { ExerciseId = id });
        Assert.False((await service.ValidateActionAsync(user, start.SessionId, new() { Action = "advance" })).IsValid);
        Assert.False((await service.ValidateActionAsync(user, start.SessionId, new() { Action = "error_analysis_select", Index = 3 })).IsValid);
        await Assert.ThrowsAnyAsync<Exception>(() => service.CompleteAsync(user, start.SessionId, new()));
        await service.ValidateActionAsync(user, start.SessionId, new() { Action = "error_analysis_start" });
        Assert.False((await service.ValidateActionAsync(user, start.SessionId, new() { Action = "error_analysis_select", Index = 99 })).IsValid);
        var hint = new ExerciseActionRequest { Action = "error_analysis_hint", ActionId = Guid.NewGuid() };
        await service.ValidateActionAsync(user, start.SessionId, hint); await service.ValidateActionAsync(user, start.SessionId, hint);
        await service.ValidateActionAsync(user, start.SessionId, new() { Action = "error_analysis_select", Index = 8 });
        Assert.False((await service.ValidateActionAsync(user, start.SessionId, new() { Action = "error_analysis_select", Index = 8 })).IsValid);
        await service.ValidateActionAsync(user, start.SessionId, new() { Action = "error_analysis_finish" });
        var result = await service.CompleteAsync(user, start.SessionId, new());
        Assert.Equal(1, result.IncorrectCount); Assert.Equal(1, result.DetailedResults.GetProperty("errorAnalysisHints").GetInt32());
        Assert.True(result.DetailedResults.TryGetProperty("errorAnalysisErrors", out _));
    }
    [Fact]
    public async Task Server_time_limit_finishes_without_accepting_late_selection()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var id = await Seed(db, user);
        var service = Service(db); var start = await service.StartAsync(user, new() { ExerciseId = id });
        await service.ValidateActionAsync(user, start.SessionId, new() { Action = "error_analysis_start" });
        var session = await db.ExerciseSessions.SingleAsync(); var state = System.Text.Json.Nodes.JsonNode.Parse(session.SessionDataJson)!;
        state["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-200); session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        var response = await service.ValidateActionAsync(user, start.SessionId, new() { Action = "error_analysis_select", Index = 3 });
        Assert.True(response.IsCompleted); Assert.Null(response.IsCorrect);
        var result = await service.CompleteAsync(user, start.SessionId, new());
        Assert.Equal(180, result.TimeSpentSeconds); Assert.Equal(0m, result.Score); Assert.Equal(0, result.CorrectCount);
    }
    [Theory]
    [InlineData("ErrorAnalysis")]
    [InlineData("ERROR_ANALYSIS")]
    [InlineData("error-analysis")]
    [InlineData("error analysis")]
    public void Nested_or_aliased_engine_configuration_cannot_expose_answer_keys(string engine)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { engineConfig = new { engineType = engine,
            errors = new[] { new { wordIndex = 3, originalWord = "yalnız", errorWord = "yanlız" } }, originalText = "yalnız" } });
        var sanitized = SpeedReading.Application.Content.SpeedReadingContentSecurity.SanitizeExerciseConfiguration(json);
        Assert.DoesNotContain("originalWord", sanitized); Assert.DoesNotContain("originalText", sanitized);
    }
    [Fact]
    public async Task Server_validates_selection_and_ignores_forged_score()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var id = await Seed(db, user);
        var service = Service(db); var started = await service.StartAsync(user, new() { ExerciseId = id });
        Assert.Equal(1, started.TotalSteps);
        Assert.DoesNotContain("originalWord", started.InitialData.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("originalWord", started.Configuration.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.True((await service.ValidateActionAsync(user, started.SessionId, new() { Action = "error_analysis_start" })).IsValid);
        var actionId = Guid.NewGuid();
        var wrong = new ExerciseActionRequest { Action = "error_analysis_select", Index = 8, ActionId = actionId };
        Assert.False((await service.ValidateActionAsync(user, started.SessionId, wrong)).IsCorrect);
        await service.ValidateActionAsync(user, started.SessionId, wrong);
        Assert.True((await service.ValidateActionAsync(user, started.SessionId, new() { Action = "error_analysis_select", Index = 3 })).IsCorrect);
        var result = await service.CompleteAsync(user, started.SessionId, new() { CustomData = new() {
            ["score"] = System.Text.Json.JsonSerializer.SerializeToElement(100),
            ["accuracy"] = System.Text.Json.JsonSerializer.SerializeToElement(100),
            ["foundErrors"] = System.Text.Json.JsonSerializer.SerializeToElement(1000)
        } });
        Assert.Equal(50m, result.Accuracy); Assert.Equal(95m, result.Score); Assert.Null(result.RawWPM);
        Assert.Single(db.ExerciseSessionResults);
        var replay = await service.CompleteAsync(user, started.SessionId, new());
        Assert.Equal(result.XpGained, replay.XpGained);
    }

    [Fact]
    public async Task Finish_without_selections_counts_missed_targets()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var id = await Seed(db, user);
        var service = Service(db); var start = await service.StartAsync(user, new() { ExerciseId = id });
        await service.ValidateActionAsync(user, start.SessionId, new() { Action = "error_analysis_start" });
        await service.ValidateActionAsync(user, start.SessionId, new() { Action = "error_analysis_finish" });
        var result = await service.CompleteAsync(user, start.SessionId, new());
        Assert.Equal(0m, result.Accuracy); Assert.Equal(0m, result.Score); Assert.Null(result.RawWPM);
    }

    [Fact]
    public async Task Invalid_content_cannot_start()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var id = await Seed(db, user, 99);
        await Assert.ThrowsAnyAsync<Exception>(() => Service(db).StartAsync(user, new() { ExerciseId = id }));
        Assert.Empty(db.ExerciseSessions);
    }

    [Fact]
    public async Task Paused_time_is_excluded_and_foreign_or_paused_actions_are_rejected()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var id = await Seed(db, user);
        var service = Service(db); var start = await service.StartAsync(user, new() { ExerciseId = id });
        await service.ValidateActionAsync(user, start.SessionId, new() { Action = "error_analysis_start" });
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), start.SessionId, new() { Action = "error_analysis_select", Index = 3 }));
        await service.PauseAsync(user, start.SessionId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateActionAsync(user, start.SessionId, new() { Action = "error_analysis_hint" }));
        var session = await db.ExerciseSessions.SingleAsync(); var state = System.Text.Json.Nodes.JsonNode.Parse(session.SessionDataJson)!;
        state["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-200); state["readingPausedAt"] = DateTime.UtcNow.AddSeconds(-199);
        session.SetState(state.ToJsonString()); await db.SaveChangesAsync(); await service.ResumeAsync(user, start.SessionId);
        var selected = await service.ValidateActionAsync(user, start.SessionId, new() { Action = "error_analysis_select", Index = 3 });
        Assert.True(selected.IsCorrect); Assert.True(selected.IsCompleted);
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static ISpeedReadingExerciseSessions Service(OwnedSpeedReadingDbContext db) => (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
    private static async Task<Guid> Seed(OwnedSpeedReadingDbContext db, Guid user, int target = 3)
    {
        var type = ExerciseType.Create(Guid.NewGuid(), "ErrorAnalysis", "Hata Analizi", "error_analysis");
        var config = System.Text.Json.JsonSerializer.Serialize(new { engineType = "error_analysis", engineConfig = new {
            words = new[] { new { index = 3, text = "yanlız" }, new { index = 8, text = "bugün" } },
            errors = new[] { new { wordIndex = target, originalWord = "yalnız", errorWord = "yanlız" } }
        } });
        var exercise = Exercise.Create("Hata Analizi", "error_analysis", config, 2, user, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync(); return exercise.Id;
    }
}
