using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

/// <summary>Live test 19.11.2026 (A12): an assignment without a score scale is graded out of 100.</summary>
public sealed class CoachingAssignmentDefaultScaleTests
{
    [Fact]
    public void A_grade_without_a_scale_is_accepted_up_to_100_and_refused_above()
    {
        var studentId = Guid.NewGuid();
        var assignment = Assignment.Create(Guid.NewGuid(), "Ölçeksiz ödev", DateTime.UtcNow.AddDays(2),
            AssignmentType.Individual, Guid.NewGuid());
        assignment.AssignToStudent(studentId);
        assignment.SubmitAssignment(studentId, "Tamamlandı");

        assignment.MaxScore.Should().BeNull();
        assignment.GradeAssignment(studentId, 85).Should().BeTrue();
        FluentActions.Invoking(() => assignment.GradeAssignment(studentId, 150))
            .Should().Throw<ArgumentException>().WithMessage("*maximum score 100*");
        Assignment.DefaultMaxScore.Should().Be(100m);
    }
}
