using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class ChildProgramPackTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Complete_schedule_has_eighty_tasks_and_real_third_week_progression(int level)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "content-packs"))) root = root.Parent;
        using var exercises = JsonDocument.Parse(File.ReadAllText(Path.Combine(root!.FullName, "content-packs", "child-exercises", "v1", "catalog.json")));
        using var programs = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "content-packs", "child-programs", "v1", "programs.json")));
        var actor = Guid.NewGuid();
        var age = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var types = new Dictionary<string, ExerciseType>();
        foreach (var e in exercises.RootElement.EnumerateArray())
        {
            var name = e.GetProperty("type").GetString()!;
            if (!types.ContainsKey(name))
            {
                types[name] = ExerciseType.Create(Guid.NewGuid(), name, name, e.GetProperty("configuration").GetProperty("engineType").GetString()!);
                db.ExerciseTypes.Add(types[name]);
            }
            db.Exercises.Add(Exercise.Create(e.GetProperty("title").GetString()!, name, e.GetProperty("configuration").GetRawText(), e.GetProperty("difficultyLevel").GetInt32(), actor, types[name].Id, targetAgeGroupId: age, id: e.GetProperty("id").GetGuid()));
        }
        foreach (var p in programs.RootElement.EnumerateArray())
            db.ProgramTemplates.Add(ProgramTemplate.Import(p.GetProperty("id").GetGuid(), p.GetProperty("name").GetString()!, "Test", age, 0, 100, p.GetProperty("plan").GetRawText(), p.GetProperty("start").GetInt32(), 2, p.GetProperty("max").GetInt32(), 4, 28, true, p.GetProperty("start").GetInt32(), 0, null, false, DateTime.UtcNow, actor.ToString(), null, null));
        await db.SaveChangesAsync();
        var template = await db.ProgramTemplates.SingleAsync(p => p.InitialDifficultyLevel == level);
        ProgramWeeklyPatternRules.Validate(template.WeeklyPatternJson, false, true);
        var assembly = typeof(OwnedSpeedReadingDbContext).Assembly;
        var builder = assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingProgramSchedule")!;
        var scheduleTask = (Task<string>)builder.GetMethod("BuildAsync")!.Invoke(null, [db, template, null, CancellationToken.None])!;
        using var schedule = JsonDocument.Parse(await scheduleTask);
        var slots = schedule.RootElement.EnumerateArray().ToArray();
        Assert.Equal(80, slots.Length);
        foreach (var slot in slots)
        {
            var week = slot.GetProperty("WeekNumber").GetInt32();
            var exercise = await db.Exercises.SingleAsync(e => e.Id == slot.GetProperty("ExerciseId").GetGuid());
            Assert.Equal(week <= 2 ? level : Math.Min(level + 1, 5), exercise.DifficultyLevel);
        }
        var serviceType = assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssessment")!;
        var service = Activator.CreateInstance(serviceType, db, null)!;
        var selection = (Task<ProgramTemplate?>)serviceType.GetMethod("FindBaselineTemplateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(service, [age, 100m, CancellationToken.None, level])!;
        Assert.Equal(template.Id, (await selection)!.Id);
        var progress = StudentProgramProgress.Start(Guid.NewGuid(), actor, template, 0, 0, actor, DateTime.UtcNow);
        for (var day = 1; day <= 14; day++)
            progress.ApplyExerciseCompletion(80, false, 1, 1, template, actor, DateTime.UtcNow.AddDays(day));
        Assert.Equal(3, progress.CurrentWeek);
        Assert.Equal(Math.Min(level + 1, 5), progress.CurrentDifficultyLevel);
    }
}
