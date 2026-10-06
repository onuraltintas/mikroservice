using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Visualization;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingVisualizationPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Selects_matching_scenes_and_persists_only_verified_answers(bool questions)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // Disposable Testcontainers database, not any production database.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync(); var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Visualization", "Görselleştirme", "visualization");
            var exercise = Exercise.Create("Görselleştirme", "strategy", "{}", 3, student, type.Id);
            var scene = VisualizationScene.Create(Guid.NewGuid(), exercise.Id, "Kırmızı bir ev", null, 5, 0, 3, null, student, DateTime.UtcNow);
            var wrong = VisualizationScene.Create(Guid.NewGuid(), exercise.Id, "Başka seviye", null, 5, 1, 1, null, student, DateTime.UtcNow);
            var question = VisualizationQuestion.Create(Guid.NewGuid(), scene.Id, "Ev ne renk?", """["Kırmızı","Mavi"]""", "A", "color", 0, null, student, DateTime.UtcNow);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.VisualizationScenes.AddRange(scene, wrong);
            if (questions) db.VisualizationQuestions.Add(question);
            await db.SaveChangesAsync();
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
                .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.Single(started.InitialData.GetProperty("visualizationScenes").EnumerateArray());
            Assert.DoesNotContain("correctAnswer", started.InitialData.GetRawText(), StringComparison.OrdinalIgnoreCase);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CompleteAsync(Guid.NewGuid(), started.SessionId, new()));
            if (questions)
            {
                var request = new ExerciseActionRequest { Action = "answer_question", QuestionId = question.Id, Answer = "A" };
                Assert.True((await service.ValidateActionAsync(student, started.SessionId, request)).IsCorrect);
                Assert.True((await service.ValidateActionAsync(student, started.SessionId, request)).IsValid);
            }
            var result = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(result.RawWPM); Assert.Null(result.WordsRead);
            if (questions) Assert.Equal(100m, result.ComprehensionScore);
            else Assert.Null(result.ComprehensionScore);
            db.ChangeTracker.Clear();
            var repeated = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Equal(result.ComprehensionScore, repeated.ComprehensionScore);
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
