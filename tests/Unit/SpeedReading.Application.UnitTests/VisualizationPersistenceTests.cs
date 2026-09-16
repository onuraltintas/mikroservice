using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new OwnedSpeedReadingDbContext(options);

        var actorId = Guid.NewGuid();
        var exerciseTypeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        context.ExerciseTypes.Add(ExerciseType.Create(
            exerciseTypeId, "visualization", "Görselleştirme", "visualization"));
        context.Exercises.Add(Exercise.Create(
            "Görsel takip", "visualization", "{}", 1, actorId, exerciseTypeId, exerciseId));
        context.VisualizationScenes.Add(VisualizationScene.Create(
            sceneId, exerciseId, "Sahne", null, 30, 0, 1, null, actorId, DateTime.UtcNow));
        context.VisualizationQuestions.Add(VisualizationQuestion.Create(
            Guid.NewGuid(), sceneId, "Ne gördünüz?", "{}", "A", "detail", 0, null, actorId, DateTime.UtcNow));
        await context.SaveChangesAsync();

        var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingVisualization")!;
        var service = (ISpeedReadingVisualization)Activator.CreateInstance(
            serviceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [context],
            culture: null)!;

        var result = await service.GetAdminScenesAsync(1, 25, null, null, CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].Questions.Should().ContainSingle();
        result.Items[0].Questions[0].Options.Should().BeEmpty();
    }
}
