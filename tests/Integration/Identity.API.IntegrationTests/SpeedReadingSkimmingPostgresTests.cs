using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingSkimmingPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Persists_main_idea_answers_and_inspection_without_fake_reading_speed(bool timedOut)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // This fixture supplies only a disposable Testcontainers database.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Skimming", "Göz Gezdirme", "skimming");
            var exercise = Exercise.Create("Göz Gezdirme", "reading",
                """{"engineConfig":{"timeLimitSeconds":90,"timing":{"minReadingTimeMs":3000}}}""", 3, student, type.Id);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
            var questions = new Dictionary<Guid, Guid>();
            for (var index = 0; index < 2; index++)
            {
                var text = ReadingText.Create(Guid.NewGuid(), "Okuma", "Kitaplar yeni düşünceler kazandırır.", difficultyLevel: 3);
                db.ReadingTexts.Add(text);
                var question = Guid.NewGuid(); questions[text.Id] = question;
                db.ReadingQuestions.Add(ReadingQuestion.Create(question, text.Id, "Ana fikir?", "A", 0, 1, 2, 3,
                    optionA: "Okumanın katkısı", optionB: "B", optionC: "C", optionD: "D"));
            }
            var wrong = ReadingText.Create(Guid.NewGuid(), "Yanlış", "yanlış seviye", difficultyLevel: 1);
            db.ReadingTexts.Add(wrong); await db.SaveChangesAsync();
            var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.StartAsync(student, new() { ExerciseId = exercise.Id, ReadingTextId = wrong.Id }));
            var start = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.Equal(1, start.InitialData.GetProperty("questions").GetArrayLength());
            Assert.Throws<KeyNotFoundException>(() => start.InitialData.GetProperty("questions")[0].GetProperty("correctAnswer"));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), start.SessionId, new() { Action = "start_reading" }));
            Assert.True((await service.ValidateActionAsync(student, start.SessionId, new() { Action = "start_reading" })).IsValid);
            Assert.False((await service.ValidateActionAsync(student, start.SessionId, new() { Action = "finish_reading", IsTimeout = true })).IsValid);
            var session = await db.ExerciseSessions.SingleAsync(); var textId = session.ReadingTextId!.Value;
            var state = JsonNode.Parse(session.SessionDataJson)!;
            state["readingStartTime"] = DateTime.UtcNow.AddSeconds(timedOut ? -91 : -6);
            session.SetState(state.ToJsonString()); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            Assert.True((await service.ValidateActionAsync(student, start.SessionId, new() { Action = "finish_reading", IsTimeout = timedOut })).IsValid);
            Assert.True((await service.ValidateActionAsync(student, start.SessionId, new() { Action = "answer_question", QuestionId = questions[textId], Answer = "A" })).IsValid);
            var result = await service.CompleteAsync(student, start.SessionId, new());
            Assert.Null(result.RawWPM); Assert.Null(result.WordsRead); Assert.Null(result.WeightedKDP);
            Assert.Equal(100m, result.ComprehensionScore);
            Assert.Equal(timedOut, result.DetailedResults.GetProperty("readingIncomplete").GetBoolean());
            if (timedOut) Assert.Equal(0, result.XpGained);
            db.ChangeTracker.Clear();
            Assert.Null((await service.CompleteAsync(student, start.SessionId, new())).RawWPM);
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
            Assert.Equal(1, await db.ReadingSessionAnswers.CountAsync());
            Assert.Equal(0, (await db.ReadingSessions.SingleAsync()).CalculatedWpm);
            var next = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.NotEqual(textId, (await db.ExerciseSessions.SingleAsync(item => item.Id == next.SessionId)).ReadingTextId);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
