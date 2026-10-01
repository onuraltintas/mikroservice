using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Application.StudentProgram;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class StaffTrainingTests
{
    [Fact]
    public async Task Staff_can_start_training_without_assessment_and_retry_preserves_progress()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = Guid.NewGuid();
        var type = SpeedReading.Domain.Catalog.ExerciseType.Create(Guid.NewGuid(), "Fixation", "Fixation", "focus");
        db.ExerciseTypes.Add(type);
        db.Exercises.Add(SpeedReading.Domain.Catalog.Exercise.Create("Exercise", "Fixation", "{}", 1, user, type.Id));
        var template = ProgramTemplate.Import(Guid.NewGuid(), "Staff training", "", Guid.NewGuid(), 0, 100,
            "{\"week1\":{\"day1\":[{\"Type\":\"Fixation\",\"Count\":1,\"Difficulty\":1}]}}",
            1, 2, 5, 1, 1, true, 1, 0, null, false, DateTime.UtcNow, null, null, null);
        db.ProgramTemplates.Add(template);
        await db.SaveChangesAsync();
        var service = (ISpeedReadingStudentProgram)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingStudentProgram")!, db, null, null)!;
        var started = await service.StartStaffTrainingAsync(user, template.Id, CancellationToken.None);
        var repeated = await service.StartStaffTrainingAsync(user, template.Id, CancellationToken.None);
        repeated.ProgramId.Should().Be(started.ProgramId);
        var progress = await db.StudentProgramProgresses.SingleAsync();
        progress.IsStaffTraining.Should().BeTrue();
    }

    [Fact]
    public void Only_staff_training_bypasses_calendar_but_not_completed_day_sequence()
    {
        var now = DateTime.UtcNow;
        SpeedReadingDailyProgressRules.GetAvailableDay(now, now, false).Should().Be(1);
        SpeedReadingDailyProgressRules.GetAvailableDay(now, now, true).Should().Be(int.MaxValue);
    }
}
