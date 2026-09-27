using FluentAssertions;
using SpeedReading.Domain.Programs;

namespace SpeedReading.Application.UnitTests;

public sealed class AssignedProgramScheduleTests
{
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
