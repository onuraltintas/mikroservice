using FluentAssertions;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace SpeedReading.Application.UnitTests;

public sealed class AssignedProgramScheduleTests
{
    [Fact]
    public void Completed_program_cannot_be_completed_twice_and_stays_on_its_last_day()
    {
        var user = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var template = ProgramTemplate.Import(Guid.NewGuid(), "Program", "", Guid.NewGuid(),
            0, 100, "{}", 1, 2, 5, 1, 1, true, 1, 0, null, false, now, null, null, null);
        var progress = StudentProgramProgress.Start(Guid.NewGuid(), user, template, 0, 0, user, now);
        progress.ApplyExerciseCompletion(80, false, 1, 1, template, user, now);
        var completedAt = progress.CompletedDate;
        progress.ApplyExerciseCompletion(90, true, 1, 1, template, user, now.AddDays(1));

        progress.DaysCompleted.Should().Be(1);
        progress.ExercisesCompleted.Should().Be(1);
        progress.CurrentDay.Should().Be(1);
        progress.CompletedDate.Should().Be(completedAt);
        progress.AverageSuccessRate.Should().Be(80);
    }

    [Theory]
    [InlineData("{}", 0)]
    [InlineData("{\"week1\":{\"day1\":[{\"Type\":\"Fixation\",\"Count\":2,\"Difficulty\":1}]}}", 1)]
    public async Task Incomplete_catalog_cannot_create_an_assigned_schedule(string pattern, int available)
    {
        await using var db = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var age = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        db.ExerciseTypes.Add(ExerciseType.Create(typeId, "Fixation", "Fixation", "focus"));
        for (var i = 0; i < available; i++)
            db.Exercises.Add(Exercise.Create("Exercise", "Fixation", "{}", 1, Guid.NewGuid(), typeId));
        await db.SaveChangesAsync();
        var template = ProgramTemplate.Import(Guid.NewGuid(), "Program", "", age, 0, 100,
            pattern, 1, 1, 2, 1, 1, true, 1, 0, null, false, DateTime.UtcNow, null, null, null);
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingProgramSchedule")!;
        var method = type.GetMethod("BuildAsync", BindingFlags.Static | BindingFlags.Public)!;
        var action = async () => await (Task<string>)method.Invoke(null,
            [db, template, null, CancellationToken.None])!;

        await action.Should().ThrowAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>();
    }

    [Fact]
    public void Assigned_schedule_is_immutable_and_controls_program_duration()
    {
        var now = DateTime.UtcNow;
        var studentId = Guid.NewGuid();
        var template = ProgramTemplate.Import(
            Guid.NewGuid(), "Program", "", Guid.NewGuid(), 0, 100, "{}",
            1, 1, 2, 1, 1, true, 1, 1, null, false,
            now, null, null, null);
        var progress = StudentProgramProgress.Start(
            Guid.NewGuid(), studentId, template, 0, 0, studentId, now);

        progress.SetSchedule("[{\"weekNumber\":1,\"dayNumber\":1}]", studentId, now);
        progress.SetSchedule("[]", studentId, now);

        progress.ScheduleJson.Should().Contain("dayNumber");
        var first = progress.ApplyExerciseCompletion(
            80, false, 1, 1, template, studentId, now, assignedTotalDays: 2);
        first.DayCompleted.Should().BeTrue();
        first.ProgramCompleted.Should().BeFalse();
        progress.CurrentDay.Should().Be(2);

        var second = progress.ApplyExerciseCompletion(
            80, false, 1, 1, template, studentId, now.AddDays(1), assignedTotalDays: 2);
        second.ProgramCompleted.Should().BeTrue();
    }
}
