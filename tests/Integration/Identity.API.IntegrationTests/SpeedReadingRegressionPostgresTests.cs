using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingRegressionPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersistsServerAnswersWithoutInventingReadingSpeed(bool hasQuestions)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // This fixture is an isolated disposable Testcontainers database, not production.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "RegressionReduction", "Regresyon", "regression_reduction");
            var config = hasQuestions
                ? """{"engineType":"regression_reduction","engineConfig":{"readingPurpose":"evaluation"}}"""
                : "{}";
            var exercise = Exercise.Create("Regresyon", "reading", config, 3, student, type.Id);
            var text = ReadingText.Create(Guid.NewGuid(), "Text", "bir iki üç dört", difficultyLevel: 3);
            var question = Guid.NewGuid();
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.ReadingTexts.Add(text);
            if (hasQuestions) db.ReadingQuestions.Add(ReadingQuestion.Create(question, text.Id, "Soru", "A", 0, 1, 1,
                optionA: "A", optionB: "B", optionC: "C", optionD: "D"));
            await db.SaveChangesAsync();
            var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId,
                new() { Action = "start_reading" }));
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "start_reading" })).IsValid);
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" })).IsValid);
            if (hasQuestions)
            {
                var answer = await service.ValidateActionAsync(student, started.SessionId,
                    new() { Action = "answer_question", QuestionId = question, Answer = "A" });
                Assert.True(answer.IsValid, answer.Message); Assert.True(answer.IsCorrect);
            }
            var result = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(result.RawWPM);
            Assert.Null(result.WordsRead);
            if (hasQuestions) Assert.Equal(100m, result.ComprehensionScore);
            else Assert.Null(result.ComprehensionScore);
            db.ChangeTracker.Clear();
            var repeat = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(repeat.RawWPM);
            Assert.Equal(result.ComprehensionScore, repeat.ComprehensionScore);
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
