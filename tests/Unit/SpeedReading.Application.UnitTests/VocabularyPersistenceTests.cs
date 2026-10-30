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
    [Fact]
    public async Task New_quiz_prefers_unseen_words_when_an_unused_pool_exists()
    {
        await using var db = Context(); var student = Guid.NewGuid();
        var exerciseId = await Seed(db, student, "quiz");
        for (var i = 4; i < 12; i++) db.VocabularyItems.Add(VocabularyItem.Create(Guid.NewGuid(), "Kelime" + i, "Anlam" + i, null, null, null, "Genel", 2, null, student, DateTime.UtcNow));
        await db.SaveChangesAsync(); var service = Service(db);
        var first = await service.StartAsync(student, new() { ExerciseId = exerciseId });
        var usedIds = new HashSet<Guid>();
        foreach (var word in first.InitialData.GetProperty("vocabularyWords").EnumerateArray()) {
            usedIds.Add(word.GetProperty("id").GetGuid());
            Assert.True((await service.ValidateActionAsync(student, first.SessionId, new() { ActionId = Guid.NewGuid(), Action = "vocabulary_review", CustomData = new() {
                ["vocabularyItemId"] = word.GetProperty("id"), ["reviewKind"] = JsonSerializer.SerializeToElement("quiz"),
                ["questionType"] = word.GetProperty("questionType"),
                ["selectedAnswer"] = word.GetProperty(word.GetProperty("questionType").GetString() == "word" ? "definition" : "word")
            } })).IsValid);
        }
        await service.CompleteAsync(student, first.SessionId, new());
        for (var attempt = 0; attempt < 8; attempt++) {
            var next = await service.StartAsync(student, new() { ExerciseId = exerciseId });
            Assert.All(next.InitialData.GetProperty("vocabularyWords").EnumerateArray(), word => Assert.DoesNotContain(word.GetProperty("id").GetGuid(), usedIds));
            (await db.ExerciseSessions.SingleAsync(session => session.Id == next.SessionId)).Abandon(DateTime.UtcNow);
            await db.SaveChangesAsync();
        }
    }
    [Fact]
    public async Task Learn_then_quiz_uses_same_words_without_scoring_self_reports()
    {
        await using var db = Context(); var student = Guid.NewGuid();
        var exerciseId = await Seed(db, student, "learning");
        var exercise = await db.Exercises.SingleAsync();
        exercise.Update(exercise.Title, exercise.Description, exercise.TypeCode, JsonSerializer.Serialize(new {
            engineType = "vocabulary_builder", engineConfig = new { mode = "learning", learnThenQuiz = true, quizType = "mixed", vocabulary = new { count = 4, difficultyLevel = 2, requireFullPool = true } }
        }), exercise.DifficultyLevel, exercise.ExerciseTypeId, exercise.TargetAgeGroupId, student, DateTime.UtcNow);
        await db.SaveChangesAsync();
        var service = Service(db); var started = await service.StartAsync(student, new() { ExerciseId = exerciseId });
        var words = started.InitialData.GetProperty("vocabularyWords").EnumerateArray().Select(word => word.Clone()).ToArray();
        ExerciseActionRequest Action(JsonElement word, string kind) => new() { ActionId = Guid.NewGuid(), Action = "vocabulary_review", CustomData = new() {
            ["vocabularyItemId"] = word.GetProperty("id"), ["reviewKind"] = JsonSerializer.SerializeToElement(kind),
            ["questionType"] = word.GetProperty("questionType"),
            ["selectedAnswer"] = word.GetProperty(word.GetProperty("questionType").GetString() == "word" ? "definition" : "word")
        } };
        Assert.False((await service.ValidateActionAsync(student, started.SessionId, Action(words[0], "quiz"))).IsValid);
        foreach (var word in words) {
            var action = Action(word, "known");
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, action)).IsValid);
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, action)).IsValid);
            Assert.False((await service.ValidateActionAsync(student, started.SessionId, Action(word, "known"))).IsValid);
        }
        Assert.Empty(db.UserVocabularyProgresses);
        Assert.Empty(db.ExerciseSessionResults);
        await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>(() => service.CompleteAsync(student, started.SessionId, new()));
        var resumed = await service.StartAsync(student, new() { ExerciseId = exerciseId });
        Assert.Equal(started.SessionId, resumed.SessionId);
        Assert.Equal("quiz", resumed.InitialData.GetProperty("vocabularyMode").GetString());
        Assert.Equal(words.Select(w => w.GetProperty("id").GetGuid()), resumed.InitialData.GetProperty("vocabularyWords").EnumerateArray().Select(w => w.GetProperty("id").GetGuid()));
        foreach (var word in words) {
            Assert.True((await service.ValidateActionAsync(student, started.SessionId, Action(word, "quiz"))).IsValid);
            Assert.Empty(await db.UserVocabularyProgresses.ToListAsync());
        }
        var result = await service.CompleteAsync(student, started.SessionId, new());
        Assert.Equal(100m, result.Accuracy); Assert.Equal(4, result.CorrectCount);
        Assert.All(await db.UserVocabularyProgresses.ToListAsync(), p => Assert.Equal(2, p.Box));
        Assert.Single(db.ExerciseSessionResults); Assert.Null(result.RawWPM);
    }

    [Fact]
    public async Task Exiting_an_incomplete_quiz_discards_answers_without_updating_vocabulary_or_gamification()
    {
        await using var db = Context(); var student = Guid.NewGuid();
        var exerciseId = await Seed(db, student, "quiz"); var service = Service(db);
        var started = await service.StartAsync(student, new() { ExerciseId = exerciseId });
        var word = started.InitialData.GetProperty("vocabularyWords")[0];
        var action = new ExerciseActionRequest { ActionId = Guid.NewGuid(), Action = "vocabulary_review", CustomData = new() {
            ["vocabularyItemId"] = word.GetProperty("id"), ["reviewKind"] = JsonSerializer.SerializeToElement("quiz"),
            ["questionType"] = word.GetProperty("questionType"),
            ["selectedAnswer"] = word.GetProperty(word.GetProperty("questionType").GetString() == "word" ? "definition" : "word")
        } };

        Assert.True((await service.ValidateActionAsync(student, started.SessionId, action)).IsValid);
        Assert.Empty(await db.UserVocabularyProgresses.ToListAsync());
        Assert.Empty(await db.UserGamifications.ToListAsync());

        await service.DiscardIncompleteAsync(student, started.SessionId);

        Assert.False(await db.ExerciseSessions.AnyAsync(item => item.Id == started.SessionId));
        Assert.False(await db.ExerciseSessionAnswers.AnyAsync(item => item.SessionId == started.SessionId));
        Assert.Empty(await db.UserVocabularyProgresses.ToListAsync());
        Assert.Empty(await db.UserGamifications.ToListAsync());
        Assert.Empty(await db.ExerciseSessionResults.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Full_pool_setting_rejects_a_short_catalog_before_creating_session(bool pinned)
    {
        await using var db = Context(); var student = Guid.NewGuid();
        var id = await Seed(db, student, "quiz"); var exercise = await db.Exercises.SingleAsync();
        exercise.Update(exercise.Title, exercise.Description, exercise.TypeCode, JsonSerializer.Serialize(new {
            engineType = "vocabulary_builder", engineConfig = new { mode = "quiz", vocabularyItemIds = pinned ? await db.VocabularyItems.Select(word => word.Id).ToArrayAsync() : Array.Empty<Guid>(), vocabulary = new { count = 8, difficultyLevel = 2, requireFullPool = true } }
        }), exercise.DifficultyLevel, exercise.ExerciseTypeId, exercise.TargetAgeGroupId, student, DateTime.UtcNow);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).StartAsync(student, new() { ExerciseId = id }));
        Assert.Empty(db.ExerciseSessions);
    }

    [Fact]
    public async Task Quiz_rejects_out_of_order_answers_before_updating_progress()
    {
        await using var db = Context(); var student = Guid.NewGuid();
        var exerciseId = await Seed(db, student, "quiz"); var service = Service(db);
        var started = await service.StartAsync(student, new() { ExerciseId = exerciseId });
        var word = started.InitialData.GetProperty("vocabularyWords")[2];
        var response = await service.ValidateActionAsync(student, started.SessionId, new() { ActionId = Guid.NewGuid(), Action = "vocabulary_review", CustomData = new() {
            ["vocabularyItemId"] = word.GetProperty("id"), ["reviewKind"] = JsonSerializer.SerializeToElement("quiz"),
            ["questionType"] = word.GetProperty("questionType"),
            ["selectedAnswer"] = word.GetProperty(word.GetProperty("questionType").GetString() == "word" ? "definition" : "word")
        } });
        Assert.False(response.IsValid); Assert.Empty(db.UserVocabularyProgresses);
    }

    [Fact]
    public async Task Full_pool_excludes_intersecting_ambiguous_pairs_before_random_selection()
    {
        await using var db = Context(); var student = Guid.NewGuid();
        var id = await Seed(db, student, "quiz");
        db.VocabularyItems.RemoveRange(db.VocabularyItems);
        foreach (var (word, definition) in new[] { ("A", "X"), ("A", "Y"), ("B", "X"), ("C", "Z"), ("D", "T") })
            db.VocabularyItems.Add(VocabularyItem.Create(Guid.NewGuid(), word, definition, null, null, null, "Genel", 2, null, student, DateTime.UtcNow));
        var exercise = await db.Exercises.SingleAsync();
        exercise.Update(exercise.Title, exercise.Description, exercise.TypeCode, JsonSerializer.Serialize(new {
            engineType = "vocabulary_builder", engineConfig = new { mode = "quiz", vocabulary = new { count = 4, difficultyLevel = 2, requireFullPool = true } }
        }), exercise.DifficultyLevel, exercise.ExerciseTypeId, exercise.TargetAgeGroupId, student, DateTime.UtcNow);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).StartAsync(student, new() { ExerciseId = id }));
        Assert.Empty(db.ExerciseSessions);
    }

    [Fact]
    public async Task Default_configuration_uses_central_words_and_rejects_empty_catalog()
    {
        await using var db = Context(); var student = Guid.NewGuid();
        var exercise = await Seed(db, student, "learning", defaultPool: true);
        var started = await Service(db).StartAsync(student, new() { ExerciseId = exercise });
        Assert.Equal(4, started.InitialData.GetProperty("vocabularyWords").GetArrayLength());
        db.VocabularyItems.RemoveRange(db.VocabularyItems); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).StartAsync(Guid.NewGuid(), new() { ExerciseId = exercise }));
        Assert.Single(db.ExerciseSessions);
    }
    [Fact]
    public async Task Historical_vocabulary_results_do_not_expose_reading_metrics()
    {
        await using var db = Context(); var student = Guid.NewGuid(); var exercise = await Seed(db, student, "learning");
        var service = Service(db); var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        db.ExerciseSessionResults.Add(SpeedReading.Domain.Sessions.ExerciseSessionResult.Create(Guid.NewGuid(), started.SessionId, student, exercise, null, 50, 10, 300, 100, 300, 100, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var replay = await service.CompleteAsync(student, started.SessionId, new());
        Assert.Null(replay.RawWPM); Assert.Null(replay.WordsRead); Assert.Null(replay.WeightedKDP);
    }
    [Fact]
    public async Task Review_ignores_older_due_row_when_latest_progress_is_not_due()
    {
        await using var db = Context(); var student = Guid.NewGuid(); var exercise = await Seed(db, student, "review");
        var word = await db.VocabularyItems.FirstAsync();
        db.UserVocabularyProgresses.Add(UserVocabularyProgress.Create(Guid.NewGuid(), student, word.Id, DateTime.UtcNow.AddDays(-4)));
        var latest = UserVocabularyProgress.Create(Guid.NewGuid(), student, word.Id, DateTime.UtcNow);
        latest.Review(true, student, DateTime.UtcNow); db.UserVocabularyProgresses.Add(latest); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).StartAsync(student, new() { ExerciseId = exercise }));
    }
    [Fact]
    public async Task Quiz_uses_unique_content_without_rejecting_mixed_pool()
    {
        await using var db = Context(); var student = Guid.NewGuid(); var exercise = await Seed(db, student, "quiz", duplicate: true);
        db.VocabularyItems.Add(VocabularyItem.Create(Guid.NewGuid(), "farklı", "farklı anlam", null, null, null, "Genel", 2, null, student, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var result = await Service(db).StartAsync(student, new() { ExerciseId = exercise });
        Assert.Equal(2, result.InitialData.GetProperty("vocabularyWords").GetArrayLength());
    }
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
    [InlineData("learning", false)]
    [InlineData("learning", true)]
    [InlineData("quiz", false)]
    [InlineData("quiz", true)]
    public async Task Persisted_reviews_are_owned_complete_and_never_reading_speed(string mode, bool existingPoints)
    {
        await using var db = Context(); var student = Guid.NewGuid(); var exercise = await Seed(db, student, mode);
        if (existingPoints)
        {
            var previous = SpeedReading.Domain.Gamification.UserGamification.CreateDefault(Guid.NewGuid(), student, DateTime.UtcNow, student.ToString());
            previous.RecordVerifiedExerciseCompletion("SpeedReading", DateTime.UtcNow.AddDays(-1), 120, 240, 85, true, 400, student, DateTime.UtcNow);
            db.UserGamifications.Add(previous);
        }
        db.Achievements.Add(SpeedReading.Domain.Gamification.Achievement.Create(
            Guid.NewGuid(), "Kelime kutusu", "İkinci kutuya ulaş", "vocabulary", "bronze", null, "",
            "vocabulary_box", """{"box":2}""", null, null, false, 25, true, 0, student, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var originalStats = await db.UserGamifications.AsNoTracking().SingleOrDefaultAsync();
        var originalJson = JsonSerializer.Serialize(originalStats);
        var service = Service(db); var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.NotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId, new() { Action = "vocabulary_review" }));
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
        Assert.Null(result.RawWPM); Assert.Null(result.WordsRead);
        // H21 (decision 30.10.2026): only the quiz is a measurement; "biliyorum/bilmiyorum" is the student's own report.
        if (mode == "quiz") { Assert.Equal(100m, result.Accuracy); Assert.Equal("Measured", result.MeasurementStatus); }
        else
        {
            Assert.Null(result.Accuracy); Assert.Null(result.Score); Assert.Equal("NotMeasured", result.MeasurementStatus);
            Assert.False((await db.ExerciseSessionResults.SingleAsync()).IsMeasured);
        }
        Assert.Equal(4, await db.UserVocabularyProgresses.CountAsync());
        if (mode == "quiz")
        {
            var stats = Assert.Single(await db.UserGamifications.ToListAsync());
            Assert.Equal(4, stats.TotalVocabularyQuestionsAnswered);
            Assert.Single(await db.UserAchievements.ToListAsync());
            Assert.Equal((originalStats?.TotalXP ?? 0) + 25L + result.XpGained, stats.TotalXP);
            Assert.Equal((originalStats?.TotalExercisesCompleted ?? 0) + 1, stats.TotalExercisesCompleted);
            Assert.Equal(originalStats?.TotalReadingSessionsCompleted ?? 0, stats.TotalReadingSessionsCompleted);
            Assert.Equal(originalStats?.MaxWPM ?? 0, stats.MaxWPM);
            if (originalStats is not null) Assert.Equal(originalStats.Id, stats.Id);
            await service.CompleteAsync(student, started.SessionId, new());
            Assert.Equal(4, stats.TotalVocabularyQuestionsAnswered);
            Assert.Equal((originalStats?.TotalXP ?? 0) + 25L + result.XpGained, stats.TotalXP);
            Assert.Equal((originalStats?.TotalExercisesCompleted ?? 0) + 1, stats.TotalExercisesCompleted);
            Assert.Single(await db.UserAchievements.ToListAsync());
        }
        else
        {
            Assert.Equal(originalJson, JsonSerializer.Serialize(await db.UserGamifications.AsNoTracking().SingleOrDefaultAsync()));
            Assert.Empty(await db.UserAchievements.ToListAsync());
        }
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<Guid> Seed(OwnedSpeedReadingDbContext db, Guid user, string mode, bool duplicate = false, bool defaultPool = false)
    {
        var type = ExerciseType.Create(Guid.NewGuid(), "Vocabulary", "Kelime Hazinesi", "vocabulary_builder");
        var exercise = Exercise.Create("Kelime Hazinesi", "vocabulary_builder", JsonSerializer.Serialize(new {
            engineType = "vocabulary_builder", engineConfig = new { mode, vocabulary = defaultPool ? null : new { count = 4, difficultyLevel = 2 } }
        }), 2, user, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
        for (var i = 0; i < 4; i++) db.VocabularyItems.Add(VocabularyItem.Create(Guid.NewGuid(), "Kelime" + i, duplicate ? "aynı" : "Anlam" + i, null, null, null, "Genel", 2, null, user, DateTime.UtcNow));
        await db.SaveChangesAsync(); return exercise.Id;
    }
}
