using Coaching.Application.Attachments;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Privacy;
using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingErasureExecutionServiceTests
{
    [Fact]
    public async Task Execute_ShouldDeleteOnlySubjectDataAndRemainIdempotent()
    {
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();
        context.AcademicGoals.AddRange(
            AcademicGoal.Create(subjectId, "Silinecek hedef", GoalCategory.SubjectMastery),
            AcademicGoal.Create(otherStudentId, "Korunacak hedef", GoalCategory.SubjectMastery));
        await context.SaveChangesAsync();
        var request = Request(subjectId);
        await new CoachingErasureAssessmentService(context, TimeProvider.System)
            .AssessAsync(AssessmentRequest(request), CancellationToken.None);
        var service = new CoachingErasureExecutionService(context, new StubAttachmentStorage(), TimeProvider.System);

        var first = await service.ExecuteAsync(request, CancellationToken.None);
        var replay = await service.ExecuteAsync(request, CancellationToken.None);

        first.DeletedRecordCount.Should().Be(1);
        replay.Id.Should().Be(first.Id);
        (await context.AcademicGoals.CountAsync(goal => goal.StudentId == subjectId)).Should().Be(0);
        (await context.AcademicGoals.CountAsync(goal => goal.StudentId == otherStudentId)).Should().Be(1);
        (await context.CoachingErasureExecutions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Execute_ShouldRejectRequestWithoutCompletedAssessment()
    {
        await using var context = CreateContext();
        var service = new CoachingErasureExecutionService(context, new StubAttachmentStorage(), TimeProvider.System);

        var action = () => service.ExecuteAsync(Request(Guid.NewGuid()), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*assessment*");
    }

    [Fact]
    public async Task Execute_ShouldRecheckLegalHoldImmediatelyBeforeDeletion()
    {
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        context.AcademicGoals.Add(AcademicGoal.Create(subjectId, "Korunacak hedef", GoalCategory.SubjectMastery));
        await context.SaveChangesAsync();
        var request = Request(subjectId);
        await new CoachingErasureAssessmentService(context, TimeProvider.System)
            .AssessAsync(AssessmentRequest(request), CancellationToken.None);
        context.CoachingLegalHolds.Add(CoachingLegalHold.Place(
            subjectId, "Yeni hukuki saklama", Guid.NewGuid(), DateTime.UtcNow));
        await context.SaveChangesAsync();

        var action = () => new CoachingErasureExecutionService(
            context, new StubAttachmentStorage(), TimeProvider.System)
            .ExecuteAsync(request, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*legal hold*");
        (await context.AcademicGoals.CountAsync(goal => goal.StudentId == subjectId)).Should().Be(1);
    }

    private static PersonalDataErasureExecutionRequestedV1 Request(Guid subjectId)
    {
        var requestId = Guid.NewGuid();
        return new PersonalDataErasureExecutionRequestedV1(
            requestId, requestId, subjectId, DateTime.UtcNow, PersonalDataScope.Coaching);
    }

    private static PersonalDataErasureAssessmentRequestedV1 AssessmentRequest(
        PersonalDataErasureExecutionRequestedV1 request) =>
        new(request.EventId, request.RequestId, request.SubjectUserId, request.AuthorizedAt,
            DryRun: true, request.Scope);

    private static CoachingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CoachingDbContext(options);
    }

    private sealed class StubAttachmentStorage : IAssignmentAttachmentStorage
    {
        public Task<AssignmentAttachmentUploadTicket> CreateUploadTicketAsync(
            Guid assignmentId, Guid studentId, Guid attachmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StoredAssignmentAttachment> StoreAsync(
            string storageKey, Stream content, string expectedContentType, long expectedSizeBytes,
            string expectedSha256, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
