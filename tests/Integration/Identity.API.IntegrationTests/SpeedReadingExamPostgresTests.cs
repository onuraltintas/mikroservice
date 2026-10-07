using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingExamPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task PersistsValidatedExamAnswersOnceWithoutReadingSpeed()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid(); var type = ExerciseType.Create(Guid.NewGuid(), "ExamSimulation", "Sınav", "exam_simulation");
            var exercise = Exercise.Create("Sınav", "exam_simulation", """{"timing":{"questionTimeSeconds":30}}""", 3, student, type.Id);
            var text = ReadingText.Create(Guid.NewGuid(), "Metin", "Okuma yeni düşünceler kazandırır.", difficultyLevel: 3);
            var question = Guid.NewGuid();
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.ReadingTexts.Add(text);
            db.ReadingQuestions.Add(ReadingQuestion.Create(question, text.Id, "Ana fikir?", "A", 0, 1, 2, 3,
                optionA: "Okumanın katkısı", optionB: "Ulaşım", optionC: "Spor", optionD: "Hava"));
            await db.SaveChangesAsync();
            var implementation = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(implementation, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var start = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.Equal(30, start.InitialData.GetProperty("examQuestionTimeSeconds").GetInt32());
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), start.SessionId, new() { Action = "exam_question_start", QuestionId = question }));
            Assert.False((await service.ValidateActionAsync(student, start.SessionId, new() { Action = "answer_question", QuestionId = question, Answer = "A" })).IsValid);
            Assert.True((await service.ValidateActionAsync(student, start.SessionId, new() { Action = "exam_question_start", QuestionId = question })).IsValid);
            Assert.True((await service.ValidateActionAsync(student, start.SessionId, new() { Action = "answer_question", QuestionId = question, Answer = "A", ResponseTime = 999999 })).IsValid);
            var result = await service.CompleteAsync(student, start.SessionId, new());
            Assert.Null(result.RawWPM); Assert.Equal(100, result.ComprehensionScore);
            await service.CompleteAsync(student, start.SessionId, new()); db.ChangeTracker.Clear();
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
