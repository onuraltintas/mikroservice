using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingEarlyWarningRepositoryTests
{
    [Fact]
    public async Task EarlyWarningMetrics_ShouldRespectTenantAndDateScope()
    {
        await using var context = CreateContext();
        var institutionId = Guid.NewGuid();
        var otherInstitutionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();
        var fromDate = DateTime.UtcNow.AddDays(-1);
        var toDate = DateTime.UtcNow.AddDays(2);

        var assignment = Assignment.Create(
            Guid.NewGuid(),
            "Ödev",
            DateTime.UtcNow.AddDays(1),
            AssignmentType.Individual,
            institutionId);
        assignment.SetScoring(100);
        assignment.AssignToStudent(studentId);
        assignment.SubmitAssignment(studentId);
        assignment.GradeAssignment(studentId, 80);

        var otherAssignment = Assignment.Create(
            Guid.NewGuid(),
            "Diğer kurum ödevi",
            DateTime.UtcNow.AddDays(1),
            AssignmentType.Individual,
            otherInstitutionId);
        otherAssignment.AssignToStudent(otherStudentId);
        otherAssignment.SubmitAssignment(otherStudentId);

        var session = CoachingSession.Create(
            Guid.NewGuid(),
            "Seans",
            DateTime.UtcNow,
            SessionType.OneOnOne,
            institutionId: institutionId);
        session.AddStudent(studentId);
        session.RecordAttendance(studentId, attended: true);

        var goal = AcademicGoal.Create(studentId, "Hedef", GoalCategory.ExamPreparation);
        goal.UpdateProgress(40);

        context.AddRange(assignment, otherAssignment, session, goal);
        await context.SaveChangesAsync();

        var result = await new CoachingEarlyWarningRepository(context)
            .GetStudentMetricsAsync(
                institutionId,
                new[] { studentId, otherStudentId },
                null,
                fromDate,
                toDate,
                CancellationToken.None);

        result.Should().HaveCount(2);
        var metrics = result.Single(item => item.StudentId == studentId);
        metrics.StudentId.Should().Be(studentId);
        metrics.AssignmentCount.Should().Be(1);
        metrics.SubmittedAssignmentCount.Should().Be(1);
        metrics.GradedAssignmentCount.Should().Be(1);
        metrics.AverageAssignmentPercentage.Should().Be(80m);
        metrics.RecordedAttendanceCount.Should().Be(1);
        metrics.AttendedSessionCount.Should().Be(1);
        metrics.GoalCount.Should().Be(1);
        metrics.AverageGoalProgress.Should().Be(40);
        result.Single(item => item.StudentId == otherStudentId)
            .AssignmentCount.Should().Be(0);
    }

    private static CoachingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CoachingDbContext(options);
    }
}
