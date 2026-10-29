using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.Visualization;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Visualization;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class VisualizationPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Three_scene_sessions_rotate_unused_content_and_keep_resumed_snapshot(bool configured)
    {
        await using var db = Context(); var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Visualization", "Görselleştirme", "visualization");
        var ids = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        var config = configured ? System.Text.Json.JsonSerializer.Serialize(new { scenes = ids.Select((id, i) =>
            new { sceneId = id, description = $"Sahne {i}", difficultyLevel = 3, displayOrder = i }) }) : "{}";
        var exercise = Exercise.Create("Görselleştirme", "strategy", config, 3, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
        if (!configured) db.VisualizationScenes.AddRange(ids.Select((id, i) =>
            VisualizationScene.Create(id, exercise.Id, $"Sahne {i}", null, 30, i, 3, null, student, DateTime.UtcNow)));
        await db.SaveChangesAsync(); var service = Service(db);
        var first = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        var firstIds = SceneIds(first);
        Assert.Equal(3, firstIds.Length);
        var resumed = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        Assert.Equal(first.SessionId, resumed.SessionId); Assert.Equal(firstIds, SceneIds(resumed));
        await service.CompleteAsync(student, first.SessionId, new());
        var second = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        Assert.Equal(3, SceneIds(second).Length);
        Assert.Empty(firstIds.Intersect(SceneIds(second)));
        await service.CompleteAsync(student, second.SessionId, new());
        var third = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        Assert.Equal(3, SceneIds(third).Distinct().Count());
        Assert.All(SceneIds(third), id => Assert.Contains(id, ids.Select(value => value.ToString("D"))));
    }

    private static string[] SceneIds(StartExerciseSessionResponse response) => response.InitialData
        .GetProperty("visualizationScenes").EnumerateArray().Select(scene => scene.GetProperty("sceneId").GetString()!).ToArray();

    [Theory]
    [InlineData("spatial", "B")]
    [InlineData("sequence", "Mavi")]
    [InlineData("detail", "b")]
    public async Task Admin_can_edit_catalog_question_types_and_grade_letter_or_text_answers(string questionType, string answer)
    {
        await using var db = Context(); var actor = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Visualization", "Görselleştirme", "visualization");
        var exercise = Exercise.Create("Görselleştirme", "strategy", "{}", 3, actor, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
        var admin = (ISpeedReadingVisualization)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingVisualization")!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        var request = new VisualizationSceneRequest(exercise.Id, "Mavi ev", null, 5, 0, 3,
            [new("Ev ne renk?", ["Kırmızı", "Mavi"], answer, questionType, 0, null)], Mode: "assessment");
        var id = await admin.CreateSceneAsync(request, actor, "create-visualization-scene", CancellationToken.None);
        Assert.Equal(id, await admin.CreateSceneAsync(request, actor, "create-visualization-scene", CancellationToken.None));
        var stored = (await admin.GetSceneAsync(id, CancellationToken.None))!;
        Assert.Equal("Mavi", stored.Questions[0].CorrectAnswer);
        var edit = request with { Questions = stored.Questions.Select(q => new VisualizationQuestionRequest(
            q.QuestionText, q.Options, q.CorrectAnswer, q.QuestionType, q.DisplayOrder, q.HintText, q.Id)).ToArray() };
        Assert.True(await admin.UpdateSceneAsync(id, edit, actor, "edit-visualization-scene", CancellationToken.None));
        Assert.True(await admin.UpdateSceneAsync(id, edit, actor, "edit-visualization-scene", CancellationToken.None));
        var started = await Service(db).StartAsync(actor, new() { ExerciseId = exercise.Id });
        var question = (await admin.GetSceneAsync(id, CancellationToken.None))!.Questions[0];
        Assert.True((await Service(db).ValidateActionAsync(actor, started.SessionId,
            new() { Action = "answer_question", QuestionId = question.Id, Answer = "B" })).IsCorrect);
    }

    [Theory]
    [InlineData("A", "A")]
    [InlineData("b", "B")]
    [InlineData(" Mavi ", "B")]
    [InlineData("C", null)]
    [InlineData("Bilinmeyen", null)]
    [InlineData("", null)]
    public void Answer_key_supports_only_existing_options(string answer, string? expected) =>
        Assert.Equal(expected, VisualizationQuestion.ResolveAnswerKey(["Kırmızı", "Mavi"], answer));

    [Theory]
    [InlineData("key", "B")]
    [InlineData("text", "A")]
    [InlineData("auto", null)]
    [InlineData("invalid", null)]
    public void Letter_options_have_explicit_answer_format(string format, string? expected) =>
        Assert.Equal(expected, VisualizationQuestion.ResolveAnswerKey(["B", "A", "X", "Y"], "B", format));

    [Fact]
    public async Task Admin_scene_listing_survives_malformed_question_options()
    {
        await using var context = Context();
        var actorId = Guid.NewGuid(); var typeId = Guid.NewGuid(); var exerciseId = Guid.NewGuid(); var sceneId = Guid.NewGuid();
        context.ExerciseTypes.Add(ExerciseType.Create(typeId, "visualization", "Görselleştirme", "visualization"));
        context.Exercises.Add(Exercise.Create("Görsel takip", "visualization", "{}", 1, actorId, typeId, exerciseId));
        context.VisualizationScenes.Add(VisualizationScene.Create(sceneId, exerciseId, "Sahne", null, 30, 0, 1, null, actorId, DateTime.UtcNow));
        context.VisualizationQuestions.Add(VisualizationQuestion.Create(Guid.NewGuid(), sceneId, "Ne gördünüz?", "{}", "A", "detail", 0, null, actorId, DateTime.UtcNow));
        await context.SaveChangesAsync();
        var service = (ISpeedReadingVisualization)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingVisualization")!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [context], null)!;
        var result = await service.GetAdminScenesAsync(1, 25, null, null, CancellationToken.None);
        Assert.Single(result.Items); Assert.Single(result.Items[0].Questions); Assert.Empty(result.Items[0].Questions[0].Options);
    }
    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static ISpeedReadingExerciseSessions Service(OwnedSpeedReadingDbContext db) =>
        (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;

    [Fact]
    public async Task Selects_only_matching_level_scenes_and_records_server_answers_without_wpm()
    {
        await using var db = Context(); var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Visualization", "Görselleştirme", "visualization");
        var exercise = Exercise.Create("Görselleştirme", "strategy", "{}", 3, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
        var wrong = VisualizationScene.Create(Guid.NewGuid(), exercise.Id, "Yanlış seviye", null, 5, 0, 1, null, student, DateTime.UtcNow);
        var scene = VisualizationScene.Create(Guid.NewGuid(), exercise.Id, "Doğru seviye", null, 5, 1, 3, null, student, DateTime.UtcNow);
        db.VisualizationScenes.AddRange(wrong, scene);
        await db.SaveChangesAsync();
        var service = Service(db); var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        var state = JsonNode.Parse((await db.ExerciseSessions.SingleAsync()).SessionDataJson)!;
        Assert.Single(state["visualizationScenes"]!.AsArray());
        Assert.Equal(scene.Id.ToString("D"), state["visualizationScenes"]![0]!["sceneId"]!.GetValue<string>());
        var result = await service.CompleteAsync(student, started.SessionId, new());
        Assert.Null(result.RawWPM); Assert.Null(result.ComprehensionScore);
    }

    [Fact]
    public async Task Rejects_empty_scene_catalog_before_creating_a_session()
    {
        await using var db = Context(); var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Visualization", "Görselleştirme", "visualization");
        var exercise = Exercise.Create("Görselleştirme", "strategy", "{}", 3, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).StartAsync(student, new() { ExerciseId = exercise.Id }));
        Assert.Empty(await db.ExerciseSessions.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Configured_scene_selection_uses_declared_level_and_excludes_blank_content(bool nested)
    {
        await using var db = Context(); var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Visualization", "Görselleştirme", "visualization");
        var config = """{"scenes":[{"sceneId":"wrong","description":"Yanlış","difficultyLevel":1},{"sceneId":"bad-level","description":"Geçersiz seviye","difficultyLevel":0},{"sceneId":"bad-age","description":"Geçersiz yaş","targetAgeGroupId":"invalid"},{"sceneId":"blank","description":" "},{"sceneId":"right","description":"Doğru","difficultyLevel":3}]}""";
        var exercise = Exercise.Create("Görselleştirme", "strategy", nested ? "{\"engineConfig\":" + config + "}" : config, 3, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
        var started = await Service(db).StartAsync(student, new() { ExerciseId = exercise.Id });
        var scenes = JsonNode.Parse(started.InitialData.GetRawText())!["visualizationScenes"]!.AsArray();
        Assert.Single(scenes); Assert.Equal("right", scenes[0]!["sceneId"]!.GetValue<string>());
    }

    [Fact]
    public async Task Server_answer_keys_are_hidden_and_completion_is_idempotent_without_reading_speed()
    {
        await using var db = Context(); var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Visualization", "Görselleştirme", "visualization");
        var exercise = Exercise.Create("Görselleştirme", "strategy", "{}", 3, student, type.Id);
        var scene = VisualizationScene.Create(Guid.NewGuid(), exercise.Id, "Kırmızı bir ev", null, 5, 0, 3, null, student, DateTime.UtcNow);
        var question = VisualizationQuestion.Create(Guid.NewGuid(), scene.Id, "Ev ne renk?", """["Kırmızı","Mavi"]""", "A", "detail", 0, null, student, DateTime.UtcNow);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.VisualizationScenes.Add(scene); db.VisualizationQuestions.Add(question);
        await db.SaveChangesAsync(); var service = Service(db);
        var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        Assert.DoesNotContain("correctAnswer", started.InitialData.GetRawText(), StringComparison.OrdinalIgnoreCase);
        await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.NotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), started.SessionId,
            new() { Action = "answer_question", QuestionId = question.Id, Answer = "A" }));
        var request = new ExerciseActionRequest { Action = "answer_question", QuestionId = question.Id, Answer = "A" };
        Assert.True((await service.ValidateActionAsync(student, started.SessionId, request)).IsCorrect);
        Assert.True((await service.ValidateActionAsync(student, started.SessionId, request)).IsValid);
        var result = await service.CompleteAsync(student, started.SessionId, new());
        Assert.Null(result.RawWPM); Assert.Equal(100m, result.ComprehensionScore);
        Assert.Equal(result.ComprehensionScore, (await service.CompleteAsync(student, started.SessionId, new())).ComprehensionScore);
        Assert.Single(await db.ExerciseSessionResults.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Limits_scenes_to_the_profile_age_group_or_global_content(bool configured)
    {
        await using var db = Context(); var student = Guid.NewGuid(); var age = Guid.NewGuid(); var otherAge = Guid.NewGuid();
        var profile = SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), student, DateTime.UtcNow);
        profile.UpdateSettings(1, 250, 75, 20, age, student, DateTime.UtcNow); db.UserProfiles.Add(profile);
        var type = ExerciseType.Create(Guid.NewGuid(), "Visualization", "Görselleştirme", "visualization");
        var configuration = configured ? System.Text.Json.JsonSerializer.Serialize(new { scenes = new[] {
            new { sceneId = "own", description = "Kendi yaş", targetAgeGroupId = (Guid?)age },
            new { sceneId = "other", description = "Başka yaş", targetAgeGroupId = (Guid?)otherAge },
            new { sceneId = "global", description = "Genel", targetAgeGroupId = (Guid?)null }
        } }) : "{}";
        var exercise = Exercise.Create("Görselleştirme", "strategy", configuration, 3, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
        if (!configured) db.VisualizationScenes.AddRange(
            VisualizationScene.Create(Guid.NewGuid(), exercise.Id, "Kendi yaş", null, 5, 0, 3, age, student, DateTime.UtcNow),
            VisualizationScene.Create(Guid.NewGuid(), exercise.Id, "Başka yaş", null, 5, 1, 3, otherAge, student, DateTime.UtcNow),
            VisualizationScene.Create(Guid.NewGuid(), exercise.Id, "Genel", null, 5, 2, 3, null, student, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var started = await Service(db).StartAsync(student, new() { ExerciseId = exercise.Id });
        var scenes = started.InitialData.GetProperty("visualizationScenes").EnumerateArray().ToArray();
        Assert.Equal(2, scenes.Length); Assert.DoesNotContain(scenes, scene => scene.GetProperty("description").GetString() == "Başka yaş");
    }

    [Fact]
    public async Task Legacy_visualization_results_hide_reading_metrics_on_replay_and_summary()
    {
        await using var db = Context(); var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Visualization", "Görselleştirme", "visualization");
        var exercise = Exercise.Create("Görselleştirme", "strategy", "{}", 3, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
        db.VisualizationScenes.Add(VisualizationScene.Create(Guid.NewGuid(), exercise.Id, "Sahne", null, 5, 0, 3, null, student, DateTime.UtcNow));
        await db.SaveChangesAsync(); var service = Service(db);
        var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        db.ExerciseSessionResults.Add(SpeedReading.Domain.Sessions.ExerciseSessionResult.Create(Guid.NewGuid(), started.SessionId,
            student, exercise.Id, null, 50, 10, 300m, 100m, 300m, 100m, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var replay = await service.CompleteAsync(student, started.SessionId, new());
        Assert.Null(replay.RawWPM); Assert.Null(replay.WordsRead); Assert.Null(replay.WeightedKDP);
        var writer = (SpeedReading.Application.Progress.ISpeedReadingProgressWriter)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingProgressWriter")!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        var summary = await writer.CreateExerciseResultAsync(student, new(exercise.Id, null, 0, 0, 0, 0, 0, null, null, SessionId: started.SessionId), "visualization-legacy-result");
        Assert.Null(summary.RawWpm); Assert.Equal(0, summary.WordsRead); Assert.Null(summary.WeightedKdp);
    }

    [Fact]
    public async Task Linked_reading_questions_do_not_replace_visualization_scene_questions_or_metrics()
    {
        await using var db = Context(); var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Visualization", "Görselleştirme", "visualization");
        var exercise = Exercise.Create("Görselleştirme", "strategy", "{}", 3, student, type.Id);
        var scene = VisualizationScene.Create(Guid.NewGuid(), exercise.Id, "Kırmızı bir ev", null, 5, 0, 3, null, student, DateTime.UtcNow);
        var question = VisualizationQuestion.Create(Guid.NewGuid(), scene.Id, "Ev ne renk?", """["Kırmızı","Mavi"]""", "A", "color", 0, null, student, DateTime.UtcNow);
        var text = ReadingText.Create(Guid.NewGuid(), "Bağlı metin", "bir iki üç", difficultyLevel: 3);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.VisualizationScenes.Add(scene); db.VisualizationQuestions.Add(question);
        db.ReadingTexts.Add(text); db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text.Id, "Metin sorusu", "A", 0, 1, 3, optionA: "Bir", optionB: "İki"));
        await db.SaveChangesAsync(); var service = Service(db);
        var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id, ReadingTextId = text.Id });
        Assert.Single(started.InitialData.GetProperty("visualizationScenes").EnumerateArray());
        Assert.True((await service.ValidateActionAsync(student, started.SessionId, new() { Action = "answer_question", QuestionId = question.Id, Answer = "A" })).IsCorrect);
        var result = await service.CompleteAsync(student, started.SessionId, new());
        Assert.Null(result.RawWPM); Assert.Null(result.WordsRead); Assert.Equal(100m, result.ComprehensionScore);
    }
}
