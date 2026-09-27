using Coaching.Application.Queries.GetCoachingAdminGoal;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAdminGoalScopeTests
{
    [Fact]
    public async Task InstitutionScopedGoalDetail_OnlyReturnsGoalsFromThatInstitution()
    {
        await using var context = new CoachingDbContext(
            new DbContextOptionsBuilder<CoachingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var institutionId = Guid.NewGuid();
        var otherInstitutionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var ownGoal = AcademicGoal.Create(
            studentId, "Current institution goal", GoalCategory.ExamPreparation, institutionId: institutionId);
        var otherGoal = AcademicGoal.Create(
            studentId, "Previous institution goal", GoalCategory.ExamPreparation, institutionId: otherInstitutionId);
        var unscopedGoal = AcademicGoal.Create(studentId, "Student-owned unscoped goal", GoalCategory.StudyHabits);
        var unrosteredGoal = AcademicGoal.Create(
            Guid.NewGuid(), "Unrostered goal", GoalCategory.ExamPreparation, institutionId: institutionId);
        context.AcademicGoals.AddRange(ownGoal, otherGoal, unscopedGoal, unrosteredGoal);
        await context.SaveChangesAsync();
        var handler = new GetCoachingAdminGoalQueryHandler(new AcademicGoalRepository(context));

        var ownResult = await handler.Handle(
            new GetCoachingAdminGoalQuery(ownGoal.Id, institutionId, true, [studentId]),
            CancellationToken.None);
        var otherResult = await handler.Handle(
            new GetCoachingAdminGoalQuery(otherGoal.Id, institutionId, true, [studentId]),
            CancellationToken.None);
        var unscopedResult = await handler.Handle(
            new GetCoachingAdminGoalQuery(unscopedGoal.Id, institutionId, true, [studentId]),
            CancellationToken.None);

        ownResult.Should().NotBeNull();
        otherResult.Should().BeNull();
        unscopedResult.Should().BeNull();
    }
}
