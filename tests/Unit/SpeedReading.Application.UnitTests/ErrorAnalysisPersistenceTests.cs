using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class ErrorAnalysisPersistenceTests
{
    [Fact]
    public async Task Server_validates_selection_and_ignores_forged_score()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var id = await Seed(db, user);
        var service = Service(db); var started = await service.StartAsync(user, new() { ExerciseId = id });
        Assert.Equal(1, started.TotalSteps);
        Assert.True((await service.ValidateActionAsync(user, started.SessionId, new() { Action = "error_analysis_start" })).IsValid);
        var actionId = Guid.NewGuid();
        var wrong = new ExerciseActionRequest { Action = "error_analysis_select", Index = 8, ActionId = actionId };
        Assert.False((await service.ValidateActionAsync(user, started.SessionId, wrong)).IsCorrect);
        await service.ValidateActionAsync(user, started.SessionId, wrong);
        Assert.True((await service.ValidateActionAsync(user, started.SessionId, new() { Action = "error_analysis_select", Index = 3 })).IsCorrect);
        var result = await service.CompleteAsync(user, started.SessionId, new());
        Assert.Equal(50m, result.Accuracy); Assert.Equal(95m, result.Score); Assert.Null(result.RawWPM);
        Assert.Single(db.ExerciseSessionResults);
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
