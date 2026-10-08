using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class AssessmentThreeTaskTests
{
    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    [InlineData(3, 8)]
    [InlineData(4, 8)]
    public async Task Three_task_form_pins_age_level_matching_text_and_all_questions(int ageIndex, int questionCount)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var age = Guid.Parse($"10000000-0000-0000-0000-00000000000{ageIndex}");
        var actor = Guid.NewGuid();
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content-packs"))) root = root.Parent;
        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(root!.FullName, "content-packs", "assessment", "v1", "catalog.json")));
        var entries = catalog.RootElement.EnumerateArray().Where(e => e.GetProperty("targetAgeGroupId").GetGuid() == age).ToArray();
        Assert.Equal(3, entries.Length);
        foreach (var e in entries)
        {
            var type = ExerciseType.Create(Guid.NewGuid(), e.GetProperty("type").GetString()!, "Test", e.GetProperty("configuration").GetProperty("engineType").GetString()!);
            db.ExerciseTypes.Add(type);
            db.Exercises.Add(Exercise.Create("Test", type.Name, e.GetProperty("configuration").GetRawText(), 1, actor, type.Id, targetAgeGroupId: age, id: e.GetProperty("id").GetGuid()));
        }
        var plan = JsonSerializer.Serialize(new { week1 = entries.Select((e, i) => new { exerciseId = e.GetProperty("id").GetGuid(), displayOrder = i + 1 }) });
        db.ProgramTemplates.Add(ProgramTemplate.Import(Guid.NewGuid(), "Assessment", "Test", age, 0, 100, plan, 1, 0, 1, 1, 1, true, 0, 0, null, true, DateTime.UtcNow, actor.ToString(), null, null));
        var text = ReadingText.Create(Guid.NewGuid(), "Suitable", "Bir metin burada okunur.", difficultyLevel: 1, targetAgeGroupId: age);
        db.ReadingTexts.Add(text);
        db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Wrong level", "Başka bir metin.", difficultyLevel: 4, targetAgeGroupId: age));
        for (var i = 0; i < questionCount; i++)
            db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text.Id, "Soru", "A", i, 1, 1, optionA: "Doğru", optionB: "Yanlış", optionC: "Diğer", optionD: "Son"));
        await db.SaveChangesAsync();
        var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssessment")!;
        var service = Activator.CreateInstance(serviceType, db, null)!;
        var method = serviceType.GetMethod("BuildPinnedFormItemsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task<IReadOnlyList<AssessmentAttemptExercise>>)method.Invoke(service,
            [Guid.NewGuid(), 3, AssessmentAttemptPhase.Baseline, "tr-baseline-v1", age, DateTime.UtcNow, actor.ToString(), CancellationToken.None])!;
        var form = await task;
        Assert.Equal(3, form.Count);
        Assert.Equal(new[] { "comprehension", "tachistoscope", "visual" }, form.Select(item => item.Role));
        Assert.Equal(text.Id, form[0].ReadingTextId);
        Assert.Null(form[1].ReadingTextId);
        Assert.Null(form[2].ReadingTextId);
        using var snapshot = JsonDocument.Parse(form[0].ContentSnapshotJson);
        Assert.Equal(questionCount, snapshot.RootElement.GetProperty("questions").GetArrayLength());
    }
}
