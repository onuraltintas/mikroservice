using Coaching.Application.Authorization;
using Coaching.Application.CoachingAgreements;
using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using MediatR;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAgreementRequirementBehaviorTests
{
    private static readonly DateTime Now = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task StudentWithoutCurrentAcknowledgement_ShouldBeBlockedBeforeHandlerRuns()
    {
        var studentId = Guid.NewGuid();
        var repository = new FakeAgreementRepository { Current = CreateDocument() };
        var behavior = CreateBehavior<ProtectedRequest>(repository, new FakeAccessPolicy(studentId, isStudent: true));
        var handlerCalled = false;

        var act = () => behavior.Handle(
            new ProtectedRequest(),
            () =>
            {
                handlerCalled = true;
                return Task.FromResult("handled");
            },
            default);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "CoachingAgreement.Required");
        handlerCalled.Should().BeFalse();
    }

    [Fact]
    public async Task StudentWithCurrentAcknowledgement_ShouldReachHandler()
    {
        var studentId = Guid.NewGuid();
        var document = CreateDocument();
        var repository = new FakeAgreementRepository
        {
            Current = document,
            Active = CoachingAgreementAcknowledgement.Create(
                document.Id,
                studentId,
                studentId,
                CoachingAgreementPartyRole.Self,
                Now.AddMinutes(-1))
        };
        var behavior = CreateBehavior<ProtectedRequest>(repository, new FakeAccessPolicy(studentId, isStudent: true));

        var result = await behavior.Handle(
            new ProtectedRequest(),
            () => Task.FromResult("handled"),
            default);

        result.Should().Be("handled");
    }

    [Fact]
    public async Task MissingPublishedAgreement_ShouldFailClosed()
    {
        var repository = new FakeAgreementRepository();
        var behavior = CreateBehavior<ProtectedRequest>(
            repository,
            new FakeAccessPolicy(Guid.NewGuid(), isStudent: true));

        var act = () => behavior.Handle(
            new ProtectedRequest(),
            () => Task.FromResult("handled"),
            default);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "CoachingAgreement.Unavailable");
    }

    [Fact]
    public async Task NonStudent_ShouldNotQueryAgreementEvidence()
    {
        var repository = new FakeAgreementRepository();
        var behavior = CreateBehavior<ProtectedRequest>(repository, new FakeAccessPolicy(Guid.NewGuid(), isStudent: false));

        var result = await behavior.Handle(
            new ProtectedRequest(),
            () => Task.FromResult("handled"),
            default);

        result.Should().Be("handled");
        repository.CurrentQueryCount.Should().Be(0);
    }

    [Fact]
    public async Task AgreementWorkflowRequest_ShouldBypassRequirementToAvoidDeadlock()
    {
        var repository = new FakeAgreementRepository { Current = CreateDocument() };
        var behavior = CreateBehavior<BypassRequest>(repository, new FakeAccessPolicy(Guid.NewGuid(), isStudent: true));

        var result = await behavior.Handle(
            new BypassRequest(),
            () => Task.FromResult("handled"),
            default);

        result.Should().Be("handled");
        repository.CurrentQueryCount.Should().Be(0);
    }

    private static CoachingAgreementRequirementBehavior<TRequest, string> CreateBehavior<TRequest>(
        FakeAgreementRepository repository,
        ICoachingAccessPolicy accessPolicy)
        where TRequest : notnull => new(repository, accessPolicy, new FixedTimeProvider(Now));

    private static CoachingAgreementDocument CreateDocument() => CoachingAgreementDocument.Publish(
        "2026.1",
        "tr-TR",
        "Öğrenci Koçluk Anlaşması",
        "https://legal.example.test/coaching/2026.1",
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        Now.AddDays(-1),
        Guid.NewGuid());

    private sealed record ProtectedRequest : IRequest<string>;
    private sealed record BypassRequest : IRequest<string>, IBypassesCoachingAgreementRequirement;

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class FakeAgreementRepository : ICoachingAgreementRepository
    {
        public CoachingAgreementDocument? Current { get; init; }
        public CoachingAgreementAcknowledgement? Active { get; init; }
        public int CurrentQueryCount { get; private set; }

        public Task<CoachingAgreementDocument?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Current?.Id == documentId ? Current : null);

        public Task<CoachingAgreementDocument?> GetCurrentAsync(string locale, DateTime asOfUtc, CancellationToken cancellationToken = default)
        {
            CurrentQueryCount++;
            return Task.FromResult(Current);
        }

        public Task<CoachingAgreementAcknowledgement?> GetActiveSelfAcknowledgementAsync(Guid documentId, Guid studentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Active);

        public Task<CoachingAgreementAcknowledgement?> GetAcknowledgementAsync(Guid acknowledgementId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CoachingAgreementAcknowledgement?>(null);

        public Task AddDocumentAsync(CoachingAgreementDocument document, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AddAcknowledgementAsync(CoachingAgreementAcknowledgement acknowledgement, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeAccessPolicy(Guid userId, bool isStudent) : ICoachingAccessPolicy
    {
        public Guid? CurrentUserId => userId;
        public bool IsSystemAdministrator => false;
        public bool IsInstitutionAdministrator => false;
        public bool IsCurrentTeacher(Guid teacherId) => false;
        public bool IsCurrentStudent(Guid studentId) => isStudent && studentId == userId;
        public Guid RequireCurrentTeacher() => throw new NotSupportedException();
        public void RequireTeacher(Guid teacherId) => throw new NotSupportedException();
        public void RequireStudent(Guid studentId) => throw new NotSupportedException();
        public void RequireTeacherOrStudent(Guid teacherId, Guid studentId) => throw new NotSupportedException();
        public void RequireTeacherOrAssignedStudent(Guid teacherId, IEnumerable<Guid> studentIds) => throw new NotSupportedException();
    }
}
