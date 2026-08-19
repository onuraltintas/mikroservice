using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAdminOverviewTests
{
    [Fact]
    public async Task Overview_ReturnsBoundedCountsAndRecentAssignments()
    {
        await using var context = CreateContext();

        var activeAssignment = Assignment.Create(
            Guid.NewGuid(),
            "Matematik tekrar",
            DateTime.UtcNow.AddDays(3),
            AssignmentType.Individual);
        activeAssignment.AssignToStudent(Guid.NewGuid());

        var completedAssignment = Assignment.Create(
            Guid.NewGuid(),
            "Deneme analizi",
            DateTime.UtcNow.AddDays(-1),
            AssignmentType.Group);
        completedAssignment.AssignToStudents(new[] { Guid.NewGuid(), Guid.NewGuid() });
        completedAssignment.Complete();

        var exam = Exam.Create(
            Guid.NewGuid(),
            "LGS denemesi",
            ExamType.Mock,
            DateTime.UtcNow,
            100);
        var session = CoachingSession.Create(
            Guid.NewGuid(),
            "Haftalık görüşme",
            DateTime.UtcNow.AddDays(1),
            SessionType.OneOnOne);
        var goal = AcademicGoal.Create(Guid.NewGuid(), "Net artırma", GoalCategory.ExamPreparation);
        goal.MarkAsCompleted();

        context.AddRange(activeAssignment, completedAssignment, exam, session, goal);
        await context.SaveChangesAsync();

        var result = await new CoachingAdminRepository(context)
            .GetOverviewAsync(1, CancellationToken.None);

        result.TotalAssignments.Should().Be(2);
        result.ActiveAssignments.Should().Be(1);
        result.CompletedAssignments.Should().Be(1);
        result.TotalAssignmentStudents.Should().Be(3);
        result.TotalExams.Should().Be(1);
        result.TotalSessions.Should().Be(1);
        result.UpcomingSessions.Should().Be(1);
        result.TotalGoals.Should().Be(1);
        result.CompletedGoals.Should().Be(1);
        result.RecentAssignments.Should().ContainSingle();
    }

    private static CoachingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CoachingDbContext(options);
    }
}
