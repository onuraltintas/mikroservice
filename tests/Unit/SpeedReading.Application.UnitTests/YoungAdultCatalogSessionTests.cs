using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Visualization;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class YoungAdultCatalogSessionTests
{
    [Theory]
    [InlineData("young-adult-exercises", "10000000-0000-0000-0000-000000000004")]
    [InlineData("adult-exercises", "10000000-0000-0000-0000-000000000003")]
    public async Task All_twenty_added_exercises_start_with_real_content_and_hidden_scene_answers(string packName, string ageGroupId)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var pack = Path.Combine(directory!.FullName, "content-packs", packName, "v1");
        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(pack, "catalog.json")));
        using var sceneCatalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(pack, "scenes.json")));
        var addedTypes = new[] { "Tachistoscope", "Visualization", "RSVP", "TextFading", "RegressionReduction", "SubvocalizationReduction" };
        var entries = catalog.RootElement.EnumerateArray().Where(e => addedTypes.Contains(e.GetProperty("type").GetString())).ToArray();
        Assert.Equal(20, entries.Length);
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var actor = Guid.NewGuid();
        var age = Guid.Parse(ageGroupId);
        var types = entries.GroupBy(e => e.GetProperty("type").GetString()!).ToDictionary(g => g.Key,
            g => ExerciseType.Create(Guid.NewGuid(), g.Key, g.Key, g.First().GetProperty("configuration").GetProperty("engineType").GetString()!));
        db.ExerciseTypes.AddRange(types.Values);
        foreach (var entry in entries)
            db.Exercises.Add(Exercise.Create(entry.GetProperty("title").GetString()!, entry.GetProperty("type").GetString()!,
                entry.GetProperty("configuration").GetRawText(), entry.GetProperty("difficultyLevel").GetInt32(), actor,
                types[entry.GetProperty("type").GetString()!].Id, targetAgeGroupId: age, id: entry.GetProperty("id").GetGuid()));
        foreach (var level in Enumerable.Range(1, 5))
        {
            var text = ReadingText.Create(Guid.NewGuid(), "Üniversitede okuma", "Araştırma yapmak farklı düşünceleri karşılaştırmayı ve kanıtları dikkatle değerlendirmeyi gerektirir.", difficultyLevel: level, targetAgeGroupId: age);
            db.ReadingTexts.Add(text);
            db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text.Id, "Araştırma ne gerektirir?", "A", 0, 1, 1,
                optionA: "Kanıtları değerlendirmek", optionB: "Uyumak", optionC: "Koşmak", optionD: "Yüzmek"));
        }
        foreach (var scene in sceneCatalog.RootElement.EnumerateArray())
        {
            db.VisualizationScenes.Add(VisualizationScene.Create(scene.GetProperty("id").GetGuid(), scene.GetProperty("exerciseId").GetGuid(),
                scene.GetProperty("description").GetString()!, null, scene.GetProperty("duration").GetInt32(),
                scene.GetProperty("displayOrder").GetInt32(), scene.GetProperty("difficultyLevel").GetInt32(), age, actor, DateTime.UtcNow));
            foreach (var question in scene.GetProperty("questions").EnumerateArray())
                db.VisualizationQuestions.Add(VisualizationQuestion.Create(question.GetProperty("id").GetGuid(), scene.GetProperty("id").GetGuid(),
                    question.GetProperty("questionText").GetString()!, question.GetProperty("options").GetRawText(), question.GetProperty("correctAnswer").GetString()!,
                    question.GetProperty("questionType").GetString()!, question.GetProperty("displayOrder").GetInt32(), null, actor, DateTime.UtcNow));
        }
        await db.SaveChangesAsync();
        var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        foreach (var entry in entries)
        {
            var student = Guid.NewGuid();
            var started = await service.StartAsync(student, new() { ExerciseId = entry.GetProperty("id").GetGuid() });
            var session = await db.ExerciseSessions.SingleAsync(s => s.Id == started.SessionId);
            var type = entry.GetProperty("type").GetString();
            if (type == "Visualization")
            {
                Assert.Equal(3, started.InitialData.GetProperty("visualizationScenes").GetArrayLength());
                Assert.DoesNotContain("correctAnswer", started.InitialData.GetRawText());
            }
            else if (type != "Tachistoscope")
            {
                Assert.NotNull(session.ReadingTextId);
                var text = await db.ReadingTexts.SingleAsync(t => t.Id == session.ReadingTextId);
                Assert.Equal(entry.GetProperty("difficultyLevel").GetInt32(), text.DifficultyLevel);
                Assert.Equal(age, text.TargetAgeGroupId);
            }
        }
    }
}
