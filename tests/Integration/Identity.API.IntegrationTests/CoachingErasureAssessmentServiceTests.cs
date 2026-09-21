using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Privacy;
using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingErasureAssessmentServiceTests
{
    [Fact]
    public async Task Assess_ShouldCountDataWithoutDeletingAndRemainIdempotent()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        context.AcademicGoals.Add(AcademicGoal.Create(
            studentId, "Matematik hedefi", GoalCategory.SubjectMastery));
        await context.SaveChangesAsync();
        var service = new CoachingErasureAssessmentService(context, TimeProvider.System);
        var message = Message(studentId);

        var first = await service.AssessAsync(message, CancellationToken.None);
        var replay = await service.AssessAsync(message, CancellationToken.None);

        first.GoalCount.Should().Be(1);
        first.CanProceed.Should().BeTrue();
        replay.Id.Should().Be(first.Id);
        (await context.AcademicGoals.CountAsync()).Should().Be(1);
        (await context.CoachingErasureAssessments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Assess_ShouldBlockWhenSubjectHasActiveLegalHold()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        context.CoachingLegalHolds.Add(CoachingLegalHold.Place(
            studentId,
            "Devam eden yasal uyuşmazlık",
            Guid.NewGuid(),
            DateTime.UtcNow));
        await context.SaveChangesAsync();

        var result = await new CoachingErasureAssessmentService(context, TimeProvider.System)
            .AssessAsync(Message(studentId), CancellationToken.None);

        result.CanProceed.Should().BeFalse();
        result.HasActiveLegalHold.Should().BeTrue();
    }

    private static PersonalDataErasureAssessmentRequestedV1 Message(Guid studentId)
    {
        var requestId = Guid.NewGuid();
        return new PersonalDataErasureAssessmentRequestedV1(
            requestId, requestId, studentId, DateTime.UtcNow, DryRun: true);
    }

    private static CoachingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CoachingDbContext(options);
    }
}
