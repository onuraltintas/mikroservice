using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingSubvocalizationPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false, "subvocalization_reduction")]
    [InlineData(true, "subvocalization_reduction")]
    [InlineData(false, "Subvocalization-Reduction")]
    public async Task Persists_only_server_answers_and_not_automatic_reading_metrics(bool questions, string engine)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // Isolated, disposable Testcontainers database; never the live database.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "SubvocalizationReduction", "İç Ses", engine);
            var configuration = questions
                ? """{"engineType":"subvocalization_reduction","engineConfig":{"readingPurpose":"evaluation"}}""" : "{}";
            var exercise = Exercise.Create("İç Ses", "reading", configuration, 3, student, type.Id);
            var text = ReadingText.Create(Guid.NewGuid(), "Text", "bir iki uc dort", difficultyLevel: 3);
            var question = Guid.NewGuid();
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.ReadingTexts.Add(text);
            if (questions) db.ReadingQuestions.Add(ReadingQuestion.Create(question, text.Id, "Soru", "A", 0, 1, 1,
                optionA: "A", optionB: "B", optionC: "C", optionD: "D"));
            await db.SaveChangesAsync();
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
                .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.DoesNotContain("correctAnswer", started.InitialData.ToString(), StringComparison.OrdinalIgnoreCase);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId,
                new() { Action = "start_reading" }));
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "start_reading" })).IsValid);
            var session = await db.ExerciseSessions.SingleAsync();
            var state = JsonNode.Parse(session.SessionDataJson)!;
            state["readingStartTime"] = DateTime.UtcNow.AddSeconds(-10);
            session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
            var finish = await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" });
            Assert.True(finish.IsValid); Assert.Null(finish.CurrentWPM);
            if (questions)
            {
                var answer = await service.ValidateActionAsync(student, started.SessionId,
                    new() { Action = "answer_question", QuestionId = question, Answer = "A" });
                Assert.True(answer.IsValid); Assert.True(answer.IsCorrect);
            }
            var result = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(result.RawWPM); Assert.Null(result.WordsRead);
            if (questions) Assert.Equal(100m, result.ComprehensionScore);
            else Assert.Null(result.ComprehensionScore);
            db.ChangeTracker.Clear();
            var repeated = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(repeated.RawWPM); Assert.Equal(result.ComprehensionScore, repeated.ComprehensionScore);
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
