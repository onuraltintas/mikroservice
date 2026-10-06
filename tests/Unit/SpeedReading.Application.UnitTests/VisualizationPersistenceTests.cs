using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.Visualization;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Visualization;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class VisualizationPersistenceTests
{
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
}
