using FluentAssertions;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingProgramAnalyticsTests
{
    [Fact]
    public void Calculates_active_program_statistics_and_distribution()
    {
        var templateId = Guid.NewGuid();
        var rows = new[]
        {
            new SpeedReadingProgramAnalyticsRow(
                Guid.NewGuid(), "Ada", "Lovelace", "ada@example.com", templateId, "Odak Programı",
                2, 7, 3, 5, 80, 2, DateTime.UtcNow, 10, 4, true),
            new SpeedReadingProgramAnalyticsRow(
                Guid.NewGuid(), "Alan", "Turing", "alan@example.com", templateId, "Odak Programı",
                2, 5, 1, 4, 60, 1, DateTime.UtcNow.AddDays(-1), 8, 3, true),
            new SpeedReadingProgramAnalyticsRow(
                Guid.NewGuid(), "Grace", "Hopper", "grace@example.com", templateId, "Odak Programı",
                1, 2, 0, 2, 20, 1, DateTime.UtcNow.AddDays(-2), 2, 1, false)
        };

        var result = SpeedReadingProgramAnalyticsCalculator.Calculate(rows);

        result.PlatformStats.TotalActiveStudents.Should().Be(2);
        result.PlatformStats.AverageSuccessRate.Should().Be(70);
        result.PlatformStats.AverageCurrentStreak.Should().Be(2);
        result.PlatformStats.TotalCompletedExercises.Should().Be(7);
        result.ProgramDistribution.Should().ContainSingle(item =>
            item.ProgramName == "Odak Programı" && item.StudentCount == 2 && item.Percentage == 100);
        result.WeeklyProgress.Should().ContainSingle(item =>
            item.WeekNumber == 2 && item.AverageProgress == 70 && item.CompletionRate == 50);
    }

    [Fact]
    public void Limits_recent_progress_to_twenty_active_rows_in_latest_activity_order()
    {
        var rows = Enumerable.Range(0, 25)
            .Select(index => new SpeedReadingProgramAnalyticsRow(
                Guid.NewGuid(), "User", index.ToString(), $"user{index}@example.com", Guid.NewGuid(), $"Program {index}",
                1, index, 0, 0, index, 1, DateTime.UtcNow.AddMinutes(-index), index, index, true))
            .ToArray();

        var result = SpeedReadingProgramAnalyticsCalculator.Calculate(rows);

        result.RecentStudentProgress.Should().HaveCount(20);
        result.RecentStudentProgress[0].StudentName.Should().Be("User 0");
        result.RecentStudentProgress[^1].StudentName.Should().Be("User 19");
    }
}
