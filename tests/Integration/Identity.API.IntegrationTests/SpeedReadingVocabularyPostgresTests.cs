using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Vocabulary;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingVocabularyPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("learning")]
    [InlineData("review")]
    [InlineData("quiz")]
    public async Task Vocabulary_records_owned_reviews_and_completes_without_reading_metrics(string mode)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        // Disposable Testcontainers database, never a live connection.
        await db.Database.EnsureDeletedAsync();
        try {
            await db.Database.EnsureCreatedAsync(); var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Vocabulary", "Kelime Hazinesi", "vocabulary_builder");
            var exercise = Exercise.Create("Kelime Hazinesi", "vocabulary_builder", JsonSerializer.Serialize(new {
                engineType = "vocabulary_builder", engineConfig = new { mode, vocabulary = new { count = 4, difficultyLevel = 2 } }
            }), 2, student, type.Id);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
            for (var i = 0; i < 4; i++) {
                var word = VocabularyItem.Create(Guid.NewGuid(), "Kelime" + i, "Anlam" + i, null, null, null, "Genel", 2, null, student, DateTime.UtcNow);
                db.VocabularyItems.Add(word);
                if (mode == "review") db.UserVocabularyProgresses.Add(UserVocabularyProgress.Create(Guid.NewGuid(), student, word.Id, DateTime.UtcNow.AddDays(-2)));
            }
            await db.SaveChangesAsync();
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            var selected = started.InitialData.GetProperty("vocabularyWords"); Assert.Equal(4, selected.GetArrayLength());
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CompleteAsync(Guid.NewGuid(), started.SessionId, new()));
            foreach (var word in selected.EnumerateArray()) {
                var custom = new Dictionary<string, JsonElement> {
                    ["vocabularyItemId"] = word.GetProperty("id").Clone(), ["reviewKind"] = JsonSerializer.SerializeToElement(mode == "quiz" ? "quiz" : "known"),
                    ["questionType"] = word.GetProperty("questionType").Clone(),
                    ["selectedAnswer"] = word.GetProperty(word.GetProperty("questionType").GetString() == "word" ? "definition" : "word").Clone()
                };
                var action = new ExerciseActionRequest { ActionId = Guid.NewGuid(), Action = "vocabulary_review", CustomData = custom };
                var response = await service.ValidateActionAsync(student, started.SessionId, action); Assert.True(response.IsValid, response.Message);
                Assert.True((await service.ValidateActionAsync(student, started.SessionId, action)).IsValid);
            }
            var result = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Equal(100m, result.Accuracy); Assert.Null(result.RawWPM); Assert.Null(result.WordsRead);
            db.ChangeTracker.Clear(); Assert.Equal(4, await db.UserVocabularyProgresses.CountAsync());
            Assert.All(await db.UserVocabularyProgresses.ToListAsync(), p => { Assert.Equal(2, p.Box); Assert.True(p.NextReviewDate > DateTime.UtcNow.AddDays(2)); });
            Assert.Null((await service.CompleteAsync(student, started.SessionId, new())).RawWPM);
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
        } finally { await db.Database.EnsureDeletedAsync(); }
    }
}
