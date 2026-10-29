using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Review H5 (finding 19, decision 29.10.2026): at the end of each program week the student's measured average sets
/// the next weeks' difficulty (≥80 up, &lt;60 down, otherwise unchanged) and the remaining days are re-selected;
/// finished days never change.
/// </summary>
public sealed class SpeedReadingAdaptiveDifficultyTests
{
    [Theory]
    [InlineData(85, 1)]
    [InlineData(80, 1)]
    [InlineData(70, 0)]
    [InlineData(60, 0)]
    [InlineData(59.9, -1)]
    [InlineData(null, 0)]
    public void The_week_average_decides_the_step(double? average, int expected) =>
        SpeedReadingDailyProgressRules.WeeklyDifficultyStep((decimal?)average).Should().Be(expected);

    [Theory]
    [InlineData(90, 1)]
    [InlineData(40, -1)]
    public async Task Remaining_weeks_are_reselected_and_finished_weeks_stay(double average, int direction)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var actor = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "RSVP", "RSVP", "text_stream");
        db.ExerciseTypes.Add(type);
        for (var level = 1; level <= 5; level++)
            db.Exercises.Add(Exercise.Create($"Seviye {level}", "rsvp", "{}", level, actor, type.Id));
        var pattern = JsonSerializer.Serialize(Enumerable.Range(1, 2).ToDictionary(week => $"week{week}",
            _ => Enumerable.Range(1, 7).ToDictionary(day => $"day{day}", _ => new[] { new { type = "RSVP", count = 1, difficulty = 3 } })));
        var template = ProgramTemplate.Import(Guid.NewGuid(), "Uyarlanabilir", "", Guid.NewGuid(), 0, 100, pattern, 3, 0, 5, 2, 14,
            true, 1, 1, null, false, DateTime.UtcNow, null, null, null);
        db.ProgramTemplates.Add(template);
        await db.SaveChangesAsync();
        var schedule = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingProgramSchedule")!;
        var build = (Task<string>)schedule.GetMethod("BuildAsync")!.Invoke(null, [db, template, null, CancellationToken.None, 0])!;
        var progress = StudentProgramProgress.Start(Guid.NewGuid(), actor, template, 0, 0, actor, DateTime.UtcNow);
        progress.SetSchedule(await build, actor, DateTime.UtcNow);
        for (var day = 1; day <= 7; day++)
            progress.ApplyExerciseCompletion((decimal)average, false, 1, 1, template, actor, DateTime.UtcNow.AddDays(day));
        progress.CurrentWeek.Should().Be(2);

        var adapt = (Task<bool>)schedule.GetMethod("AdaptAfterWeekAsync")!.Invoke(null,
            [db, progress, template, (decimal?)average, actor, DateTime.UtcNow, CancellationToken.None])!;

        (await adapt).Should().BeTrue();
        progress.AdaptiveDifficultyOffset.Should().Be(direction);
        var levels = await db.Exercises.ToDictionaryAsync(item => item.Id, item => item.DifficultyLevel);
        using var rebuilt = JsonDocument.Parse(progress.ScheduleJson!);
        foreach (var slot in rebuilt.RootElement.EnumerateArray())
        {
            var expected = slot.GetProperty("WeekNumber").GetInt32() == 1 ? 3 : 3 + direction;
            levels[slot.GetProperty("ExerciseId").GetGuid()].Should().Be(expected);
        }
    }
}
