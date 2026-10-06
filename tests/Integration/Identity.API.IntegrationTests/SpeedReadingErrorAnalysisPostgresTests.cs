using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingErrorAnalysisPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Proofreading_persists_verified_answers_hints_and_completion_once()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync(); var user = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "ErrorAnalysis", "Hata Analizi", "error_analysis");
            var config = System.Text.Json.JsonSerializer.Serialize(new { engineType = "error_analysis", engineConfig = new {
                words = new[] { new { index = 3, text = "yanlız" }, new { index = 8, text = "bugün" } },
                errors = new[] { new { wordIndex = 3, originalWord = "yalnız", errorWord = "yanlız" } }
            } });
            var exercise = Exercise.Create("Hata Analizi", "error_analysis", config, 2, user, type.Id);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(user, new() { ExerciseId = exercise.Id });
            Assert.DoesNotContain("originalWord", started.InitialData.GetRawText());
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId, new() { Action = "error_analysis_select", Index = 3 }));
            await service.ValidateActionAsync(user, started.SessionId, new() { Action = "error_analysis_start" });
            var hint = new ExerciseActionRequest { Action = "error_analysis_hint", ActionId = Guid.NewGuid() };
            await service.ValidateActionAsync(user, started.SessionId, hint); await service.ValidateActionAsync(user, started.SessionId, hint);
            db.ChangeTracker.Clear();
            await service.ValidateActionAsync(user, started.SessionId, new() { Action = "error_analysis_select", Index = 8 });
            await service.ValidateActionAsync(user, started.SessionId, new() { Action = "error_analysis_select", Index = 3 });
            db.ChangeTracker.Clear();
            var result = await service.CompleteAsync(user, started.SessionId, new());
            var replay = await service.CompleteAsync(user, started.SessionId, new());
            Assert.Equal(95m, result.Score); Assert.Equal(50m, result.Accuracy); Assert.Null(result.RawWPM);
            Assert.Equal(1, result.DetailedResults.GetProperty("errorAnalysisHints").GetInt32());
            Assert.Equal(result.XpGained, replay.XpGained); Assert.Equal(result.Score, replay.Score);
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync()); Assert.Empty(db.ReadingSessions);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
