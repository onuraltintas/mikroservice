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

    [Fact]
    public async Task Overview_TenantScope_ExcludesOtherInstitutionAndUnrosteredGoals()
    {
        await using var context = CreateContext();
        var institutionId = Guid.NewGuid();
        var otherInstitutionId = Guid.NewGuid();
        var inScopeStudentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();

        var inScopeAssignment = Assignment.Create(
            Guid.NewGuid(),
            "Kurum içi ödev",
            DateTime.UtcNow.AddDays(3),
            AssignmentType.Individual,
            institutionId);
        inScopeAssignment.AssignToStudent(inScopeStudentId);

        var otherAssignment = Assignment.Create(
            Guid.NewGuid(),
            "Diğer kurum ödevi",
            DateTime.UtcNow.AddDays(3),
            AssignmentType.Individual,
            otherInstitutionId);
        otherAssignment.AssignToStudent(otherStudentId);

        var inScopeExam = Exam.Create(
            Guid.NewGuid(),
            "Kurum sınavı",
            ExamType.Mock,
            DateTime.UtcNow,
            100,
            institutionId);
        var otherExam = Exam.Create(
            Guid.NewGuid(),
            "Diğer kurum sınavı",
            ExamType.Mock,
            DateTime.UtcNow,
            100,
            otherInstitutionId);
        var inScopeSession = CoachingSession.Create(
            Guid.NewGuid(),
            "Kurum seansı",
            DateTime.UtcNow.AddDays(1),
            SessionType.OneOnOne,
            institutionId: institutionId);
        var otherSession = CoachingSession.Create(
            Guid.NewGuid(),
            "Diğer kurum seansı",
            DateTime.UtcNow.AddDays(1),
            SessionType.OneOnOne,
            institutionId: otherInstitutionId);
        var inScopeGoal = AcademicGoal.Create(inScopeStudentId, "Kurum hedefi", GoalCategory.ExamPreparation);
        var otherGoal = AcademicGoal.Create(otherStudentId, "Diğer kurum hedefi", GoalCategory.ExamPreparation);

        context.AddRange(
            inScopeAssignment,
            otherAssignment,
            inScopeExam,
            otherExam,
            inScopeSession,
            otherSession,
            inScopeGoal,
            otherGoal);
        await context.SaveChangesAsync();

        var result = await new CoachingAdminRepository(context)
            .GetOverviewAsync(
                10,
                CancellationToken.None,
                institutionId,
                new[] { inScopeStudentId });

        result.TotalAssignments.Should().Be(1);
        result.TotalExams.Should().Be(1);
        result.TotalSessions.Should().Be(1);
        result.TotalGoals.Should().Be(1);
        result.RecentAssignments.Should().ContainSingle(item => item.InstitutionId == institutionId);
    }

    private static CoachingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CoachingDbContext(options);
    }
}
