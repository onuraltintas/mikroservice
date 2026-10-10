using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class ComprehensionModesTests
{
    [Fact]
    public async Task Learning_reading_feedback_does_not_publish_an_independent_speed()
    {
        await using var db = Context();
        var (service, user, exercise) = await Seed(db, "learning");
        var started = await service.StartAsync(user, new() { ExerciseId = exercise });
        await FinishReading(db, service, user, started.SessionId);
        var reading = await service.ValidateActionAsync(user, started.SessionId, new() { Action = "finish_reading" });
        Assert.True(reading.IsValid);
        Assert.Null(reading.CurrentWPM);
    }

    [Theory]
    [InlineData("evaluation", false)]
    [InlineData("learning", true)]
    public async Task Feedback_matches_mode_and_evaluation_keys_are_released_only_after_completion(string mode, bool immediate)
    {
        await using var db = Context();
        var (service, user, exercise) = await Seed(db, mode);
        var started = await service.StartAsync(user, new() { ExerciseId = exercise });
        Assert.False(started.InitialData.GetProperty("questions")[0].TryGetProperty("correctAnswer", out _));
        await FinishReading(db, service, user, started.SessionId);
        var id = started.InitialData.GetProperty("questions")[0].GetProperty("questionId").GetGuid();
        var feedback = await service.ValidateActionAsync(user, started.SessionId,
            new() { Action = "answer_question", QuestionId = id, Answer = "A", ResponseTime = 2500 });
        Assert.True(feedback.IsValid);
        Assert.Equal(immediate ? true : null, feedback.IsCorrect);
        Assert.Equal(immediate ? "A" : null, feedback.CorrectAnswer);
        var retry = await service.ValidateActionAsync(user, started.SessionId,
            new() { Action = "answer_question", QuestionId = id, Answer = "B" });
        Assert.Equal(immediate ? "A" : null, retry.CorrectAnswer);
        var result = await service.CompleteAsync(user, started.SessionId, new());
        Assert.Equal(100m, result.Score);
        Assert.Equal(mode, result.DetailedResults.GetProperty("comprehensionMode").GetString());
        Assert.Equal("A", result.DetailedResults.GetProperty("questions")[0].GetProperty("correctAnswer").GetString());
        Assert.Equal(immediate ? 0 : 1, db.ReadingSessions.Count());
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    public async Task Comprehension_score_does_not_reward_reading_speed(int seconds)
    {
        await using var db = Context();
        var (service, user, exercise) = await Seed(db, "evaluation");
        var started = await service.StartAsync(user, new() { ExerciseId = exercise });
        await FinishReading(db, service, user, started.SessionId, seconds);
        var question = started.InitialData.GetProperty("questions")[0].GetProperty("questionId").GetGuid();
        await service.ValidateActionAsync(user, started.SessionId, new() { Action = "answer_question", QuestionId = question, Answer = "A" });
        Assert.Equal(100m, (await service.CompleteAsync(user, started.SessionId, new())).Score);
    }

    [Fact]
    public async Task Fast_reading_cannot_raise_the_score_of_wrong_answers()
    {
        await using var db = Context();
        var (service, user, exercise) = await Seed(db, "evaluation");
        var started = await service.StartAsync(user, new() { ExerciseId = exercise });
        await FinishReading(db, service, user, started.SessionId, seconds: 10);
        await service.ValidateActionAsync(user, started.SessionId, new() { Action = "answer_question",
            QuestionId = started.InitialData.GetProperty("questions")[0].GetProperty("questionId").GetGuid(), Answer = "B" });
        var result = await service.CompleteAsync(user, started.SessionId, new());
        Assert.True(result.RawWPM > 0);
        Assert.Equal(0m, result.Score);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejects_wrong_length_in_automatic_and_pinned_selection(bool pinned)
    {
        await using var db = Context();
        var (service, user, exercise) = await Seed(db, "evaluation", wordCount: 2);
        var text = db.ReadingTexts.Single();
        await Assert.ThrowsAnyAsync<Exception>(() => service.StartAsync(user,
            new() { ExerciseId = exercise, ReadingTextId = pinned ? text.Id : null }));
        Assert.Empty(db.ExerciseSessions);
    }

    [Fact]
    public async Task Rejects_insufficient_questions_instead_of_silently_reducing_the_configured_count()
    {
        await using var db = Context();
        var (service, user, exercise) = await Seed(db, "evaluation", questionCount: 2);
        await Assert.ThrowsAnyAsync<Exception>(() => service.StartAsync(user, new() { ExerciseId = exercise }));
        Assert.Empty(db.ExerciseSessions);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Includes_inference_and_evaluation_questions_not_just_literal_questions(int questionType)
    {
        await using var db = Context();
        var (service, user, exercise) = await Seed(db, "evaluation", questionCount: 2);
        db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), db.ReadingTexts.Single().Id, "Metinden hangi sonuç çıkar?",
            "B", 1, questionType, 3, 2, "Metnin açıklamasına göre B.", "A", "B", "C", "D"));
        await db.SaveChangesAsync();
        var started = await service.StartAsync(user, new() { ExerciseId = exercise });
        Assert.Equal(2, started.InitialData.GetProperty("questions").GetArrayLength());
        Assert.Equal(questionType, started.InitialData.GetProperty("questions")[1].GetProperty("questionType").GetInt32());
    }

    [Fact]
    public void Rejects_unknown_comprehension_mode()
    {
        Assert.Throws<ArgumentException>(() => ExerciseConfigurationRules.ValidateActiveConfiguration(
            """{"engineType":"reading_comprehension","engineConfig":{"comprehensionMode":"shortcut"}}""", "reading_comprehension"));
    }

    [Fact]
    public void Rejects_inverted_word_band_even_when_the_bounds_are_in_different_scopes()
    {
        Assert.Throws<ArgumentException>(() => ExerciseConfigurationRules.ValidateActiveConfiguration(
            """{"engineType":"reading_comprehension","content":{"minWordCount":300},"engineConfig":{"content":{"maxWordCount":200}}}""", "reading_comprehension"));
    }

    [Fact]
    public async Task Learning_does_not_raise_the_independent_adaptive_comprehension_profile()
    {
        await using var db = Context();
        var (service, user, exercise) = await Seed(db, "learning");
        var started = await service.StartAsync(user, new() { ExerciseId = exercise });
        await FinishReading(db, service, user, started.SessionId);
        await service.ValidateActionAsync(user, started.SessionId, new() { Action = "answer_question",
            QuestionId = started.InitialData.GetProperty("questions")[0].GetProperty("questionId").GetGuid(), Answer = "A" });
        await service.CompleteAsync(user, started.SessionId, new());
        var implementation = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAdaptiveLearning")!;
        var adaptive = (SpeedReading.Application.AdaptiveLearning.ISpeedReadingAdaptiveLearning)Activator.CreateInstance(implementation,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        Assert.Equal(0m, (await adaptive.GetProfileAsync(user)).AverageComprehension);
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(ISpeedReadingExerciseSessions Service, Guid User, Guid Exercise)> Seed(
        OwnedSpeedReadingDbContext db, string mode, int wordCount = 100, int questionCount = 1)
    {
        var user = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Comprehension", "Anlama", "reading_comprehension");
        var config = new JsonObject { ["engineType"] = "reading_comprehension", ["engineConfig"] = new JsonObject {
            ["comprehensionMode"] = mode, ["content"] = new JsonObject {
                ["source"] = "reading_text", ["minWordCount"] = 80, ["maxWordCount"] = 120, ["questionCount"] = questionCount } } };
        var exercise = Exercise.Create("Anlama", "reading", config.ToJsonString(), 2, user, type.Id);
        var text = ReadingText.Create(Guid.NewGuid(), "Bahçe", string.Join(" ", Enumerable.Repeat("bahçe", wordCount)), difficultyLevel: 2);
        db.ExerciseTypes.Add(type);
        db.Exercises.Add(exercise);
        db.ReadingTexts.Add(text);
        db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text.Id, "Konu nedir?", "A", 0, 1, 1, 2,
            "Metin bahçeyi anlatıyor.", "Bahçe", "Okul", "Tren", "Deniz"));
        await db.SaveChangesAsync();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        return ((ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!, user, exercise.Id);
    }

    private static async Task FinishReading(OwnedSpeedReadingDbContext db, ISpeedReadingExerciseSessions service, Guid user, Guid id, int seconds = 30)
    {
        Assert.True((await service.ValidateActionAsync(user, id, new() { Action = "start_reading" })).IsValid);
        var session = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state["readingStartTime"] = DateTime.UtcNow.AddSeconds(-seconds);
        session.SetState(state.ToJsonString());
        await db.SaveChangesAsync();
        Assert.True((await service.ValidateActionAsync(user, id, new() { Action = "finish_reading" })).IsValid);
    }
}
