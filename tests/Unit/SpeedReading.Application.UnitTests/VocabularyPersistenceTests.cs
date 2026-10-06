using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Vocabulary;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class VocabularyPersistenceTests
{
    private static ISpeedReadingExerciseSessions Service(OwnedSpeedReadingDbContext db) =>
        (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;

    [Fact]
    public async Task Review_selects_only_own_due_words_and_returns_server_box()
    {
        await using var db = Context(); var student = Guid.NewGuid();
        var exercise = await Seed(db, student, "review"); var words = await db.VocabularyItems.OrderBy(w => w.Word).ToListAsync();
        db.UserVocabularyProgresses.Add(UserVocabularyProgress.Create(Guid.NewGuid(), student, words[0].Id, DateTime.UtcNow.AddDays(-2)));
        var future = UserVocabularyProgress.Create(Guid.NewGuid(), student, words[1].Id, DateTime.UtcNow);
        future.Review(true, student, DateTime.UtcNow); db.UserVocabularyProgresses.Add(future);
        db.UserVocabularyProgresses.Add(UserVocabularyProgress.Create(Guid.NewGuid(), Guid.NewGuid(), words[2].Id, DateTime.UtcNow.AddDays(-2)));
        await db.SaveChangesAsync();
        var started = await Service(db).StartAsync(student, new() { ExerciseId = exercise });
        var selected = started.InitialData.GetProperty("vocabularyWords");
        Assert.Equal(1, selected.GetArrayLength()); Assert.Equal(words[0].Id, selected[0].GetProperty("id").GetGuid());
        Assert.Equal(1, selected[0].GetProperty("box").GetInt32());
    }

    [Fact]
    public async Task Empty_review_pool_does_not_create_a_fake_completed_session()
    {
        await using var db = Context(); var student = Guid.NewGuid(); var exercise = await Seed(db, student, "review");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).StartAsync(student, new() { ExerciseId = exercise }));
        Assert.Empty(db.ExerciseSessions);
    }

    [Fact]
    public async Task Quiz_rejects_duplicate_definition_pool()
    {
        await using var db = Context(); var student = Guid.NewGuid(); var exercise = await Seed(db, student, "quiz", duplicate: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).StartAsync(student, new() { ExerciseId = exercise }));
    }

    [Theory]
    [InlineData("learning")]
    [InlineData("quiz")]
    public async Task Persisted_reviews_are_owned_complete_and_never_reading_speed(string mode)
    {
        await using var db = Context(); var student = Guid.NewGuid(); var exercise = await Seed(db, student, mode);
        var service = Service(db); var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId, new() { Action = "vocabulary_review" }));
        foreach (var word in started.InitialData.GetProperty("vocabularyWords").EnumerateArray()) {
            var custom = new Dictionary<string, JsonElement> {
                ["vocabularyItemId"] = JsonSerializer.SerializeToElement(word.GetProperty("id").GetGuid()),
                ["reviewKind"] = JsonSerializer.SerializeToElement(mode == "quiz" ? "quiz" : "known"),
                ["questionType"] = word.GetProperty("questionType").Clone(),
                ["selectedAnswer"] = word.GetProperty(word.GetProperty("questionType").GetString() == "word" ? "definition" : "word").Clone()
            };
            var action = new ExerciseActionRequest { ActionId = Guid.NewGuid(), Action = "vocabulary_review", CustomData = custom };
            var response = await service.ValidateActionAsync(student, started.SessionId, action);
            Assert.True(response.IsValid, response.Message);
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, action)).IsValid);
            Assert.False((await service.ValidateActionAsync(student, started.SessionId, new() { ActionId = Guid.NewGuid(), Action = action.Action, CustomData = custom })).IsValid);
        }
        var result = await service.CompleteAsync(student, started.SessionId, new());
        Assert.Null(result.RawWPM); Assert.Null(result.WordsRead); Assert.Equal(100m, result.Accuracy);
        Assert.Equal(4, await db.UserVocabularyProgresses.CountAsync());
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<Guid> Seed(OwnedSpeedReadingDbContext db, Guid user, string mode, bool duplicate = false)
    {
        var type = ExerciseType.Create(Guid.NewGuid(), "Vocabulary", "Kelime Hazinesi", "vocabulary_builder");
        var exercise = Exercise.Create("Kelime Hazinesi", "vocabulary_builder", JsonSerializer.Serialize(new {
            engineType = "vocabulary_builder", engineConfig = new { mode, vocabulary = new { count = 4, difficultyLevel = 2 } }
        }), 2, user, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
        for (var i = 0; i < 4; i++) db.VocabularyItems.Add(VocabularyItem.Create(Guid.NewGuid(), "Kelime" + i, duplicate ? "aynı" : "Anlam" + i, null, null, null, "Genel", 2, null, user, DateTime.UtcNow));
        await db.SaveChangesAsync(); return exercise.Id;
    }
}
