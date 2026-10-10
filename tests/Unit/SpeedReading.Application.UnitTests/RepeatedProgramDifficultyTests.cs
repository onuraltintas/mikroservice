using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class RepeatedProgramDifficultyTests
{
    [Theory]
    [InlineData(2, 2)]
    [InlineData(4, 3)]
    public async Task Repeated_slots_reuse_the_best_available_level_instead_of_falling_to_a_lower_unused_level(int requested, int available)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var age = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Tachistoscope", "Tachistoscope", "tachistoscope");
        db.ExerciseTypes.Add(type);
        db.Exercises.Add(Exercise.Create("Lower level", "Tachistoscope", "{}", available - 1, actor, type.Id, targetAgeGroupId: age));
        var best = Exercise.Create("Best available", "Tachistoscope", "{}", available, actor, type.Id, targetAgeGroupId: age);
        db.Exercises.Add(best);
        await db.SaveChangesAsync();
        var pattern = JsonSerializer.Serialize(new { week1 = new { day1 = new[]
        {
            new { type = "Tachistoscope", count = 1, difficulty = requested },
            new { type = "Tachistoscope", count = 1, difficulty = requested }
        } } });
        var template = ProgramTemplate.Import(Guid.NewGuid(), "Repeated training", "", age, 0, 100,
            pattern, requested, 0, 5, 1, 1, true, 1, 0, null, false, DateTime.UtcNow, null, null, null);
        var builder = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingProgramSchedule")!.GetMethod("BuildAsync", BindingFlags.Public | BindingFlags.Static)!;
        using var schedule = JsonDocument.Parse(await (Task<string>)builder.Invoke(null, [db, template, null, CancellationToken.None])!);
        var slots = schedule.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, slots.Length);
        Assert.All(slots, slot => Assert.Equal(best.Id, slot.GetProperty("ExerciseId").GetGuid()));
        Assert.Equal(new[] { 1, 2 }, slots.Select(slot => slot.GetProperty("Order").GetInt32()));
    }
}
