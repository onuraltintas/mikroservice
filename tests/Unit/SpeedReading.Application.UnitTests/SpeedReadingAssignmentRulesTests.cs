using FluentAssertions;
using SpeedReading.Application.Assignments;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingAssignmentRulesTests
{
    [Fact]
    public void Removes_empty_and_duplicate_student_ids()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var result = SpeedReadingAssignmentRules.NormalizeStudentIds(
            [first, Guid.Empty, first, second]);

        result.Should().Equal(first, second);
    }

    [Fact]
    public void Uses_utc_due_date_and_default_when_missing()
    {
        var specified = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Unspecified);

        SpeedReadingAssignmentRules.NormalizeDueDate(specified).Kind.Should().Be(DateTimeKind.Utc);
        SpeedReadingAssignmentRules.NormalizeDueDate(null)
            .Should().BeAfter(DateTime.UtcNow.AddDays(6));
    }
}
