using FluentAssertions;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Application.UnitTests;

public sealed class TeacherAnalyticsRulesTests
{
    [Fact]
    public void Class_summary_uses_reading_rows_for_speed_and_comprehension_but_counts_all_activity()
    {
        var readingStudent = Guid.NewGuid();
        var exerciseOnlyStudent = Guid.NewGuid();
        var samples = new[]
        {
            new TeacherMetricSample(readingStudent, 2, 300, 80, 120, true),
            new TeacherMetricSample(readingStudent, 1, 0, 90, 30, false),
            new TeacherMetricSample(exerciseOnlyStudent, 1, 0, 95, 20, false)
        };

        var result = TeacherAnalyticsRules.Summarize(samples);

        result.ActiveStudentIds.Should().BeEquivalentTo(new[] { readingStudent, exerciseOnlyStudent });
        result.TotalActivitiesCompleted.Should().Be(4);
        result.ClassAverageWpm.Should().Be(300);
        result.ClassAverageComprehension.Should().Be(80);
        result.ReadingStudents.Should().ContainSingle(item => item.StudentId == readingStudent);
        result.ReadingStudents.Should().NotContain(item => item.StudentId == exerciseOnlyStudent);
    }

    [Fact]
    public void Progress_prefers_reading_comprehension_when_both_activity_types_exist()
    {
        var studentId = Guid.NewGuid();
        var midpoint = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc);
        var samples = new[]
        {
            new TeacherProgressSample(studentId, midpoint.AddDays(-2), 70, true),
            new TeacherProgressSample(studentId, midpoint.AddDays(2), 85, true),
            new TeacherProgressSample(studentId, midpoint.AddDays(-1), 10, false),
            new TeacherProgressSample(studentId, midpoint.AddDays(1), 95, false)
        };

        var result = TeacherAnalyticsRules.CalculateProgress(samples, midpoint);

        result.Should().ContainSingle();
        result[0].PreviousScore.Should().Be(70);
        result[0].CurrentScore.Should().Be(85);
        result[0].Metric.Should().Be("comprehension");
    }

    [Fact]
    public void Progress_does_not_create_a_trend_when_one_half_has_no_measurement()
    {
        var studentId = Guid.NewGuid();
        var midpoint = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc);

        var result = TeacherAnalyticsRules.CalculateProgress(
            [new TeacherProgressSample(studentId, midpoint.AddDays(1), 82, true)],
            midpoint);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Class_summary_excludes_unmeasured_readings_from_speed_and_comprehension_averages()
    {
        var measuredStudent = Guid.NewGuid();
        var unmeasuredStudent = Guid.NewGuid();
        var result = TeacherAnalyticsRules.Summarize([
            new TeacherMetricSample(measuredStudent, 1, 300, 80, 60, true, ComprehensionActivityCount: 1),
            new TeacherMetricSample(unmeasuredStudent, 1, 240, 0, 45, true,
                ComprehensionActivityCount: 0, MeasuredActivityCount: 0)
        ]);

        result.ClassAverageWpm.Should().Be(300);
        result.ClassAverageComprehension.Should().Be(80);
        result.ReadingStudents.Should().HaveCount(2);
    }
}
