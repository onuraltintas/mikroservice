using System.Security.Claims;
using EduPlatform.Shared.Contracts.Events.Privacy;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.DataSubjectRequests;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class DataSubjectRequestErasureExecutionTests
{
    [Fact]
    public async Task Start_ShouldPublishExecutionOnlyWhenRequiredAssessmentIsReady()
    {
        var request = ApprovedCoachingRequest();
        var repository = new StubRequestRepository(request);
        var publisher = new StubExecutionPublisher();
        var handler = new StartDataSubjectRequestErasureCommandHandler(
            repository,
            new StubAssessmentRepository(Assessment(request, canProceed: true, legalHold: false)),
            new SystemAdmin(),
            new FixedTimeProvider(Utc(15)),
            publisher);

        var result = await handler.Handle(
            new StartDataSubjectRequestErasureCommand(request.Id), CancellationToken.None);

        result.Status.Should().Be(DataSubjectRequestStatus.Processing);
        publisher.Published.Should().ContainSingle(message =>
            message.RequestId == request.Id
            && message.SubjectUserId == request.RequesterUserId
            && message.Scope == PersonalDataScope.Coaching);
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Start_ShouldRejectIncompleteAssessmentWithoutChangingState()
    {
        var request = ApprovedCoachingRequest();
        var publisher = new StubExecutionPublisher();
        var handler = new StartDataSubjectRequestErasureCommandHandler(
            new StubRequestRepository(request),
            new StubAssessmentRepository(),
            new SystemAdmin(),
            new FixedTimeProvider(Utc(15)),
            publisher);

        var action = () => handler.Handle(
            new StartDataSubjectRequestErasureCommand(request.Id), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not ready*");
        request.Status.Should().Be(DataSubjectRequestStatus.Approved);
        publisher.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_ShouldRejectBlockingLegalHoldWithoutPublishing()
    {
        var request = ApprovedCoachingRequest();
        var publisher = new StubExecutionPublisher();
        var handler = new StartDataSubjectRequestErasureCommandHandler(
            new StubRequestRepository(request),
            new StubAssessmentRepository(Assessment(request, canProceed: false, legalHold: true)),
            new SystemAdmin(),
            new FixedTimeProvider(Utc(15)),
            publisher);

        var action = () => handler.Handle(
            new StartDataSubjectRequestErasureCommand(request.Id), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not ready*");
        request.Status.Should().Be(DataSubjectRequestStatus.Approved);
        publisher.Published.Should().BeEmpty();
    }

    private static DataSubjectRequest ApprovedCoachingRequest()
    {
        var request = DataSubjectRequest.Create(
            Guid.NewGuid(), DataSubjectRequestType.Erasure, PersonalDataScope.Coaching,
            "Koçluk verilerimi sil", Utc(12));
        request.VerifyIdentity(Utc(13));
        request.Approve(Guid.NewGuid(), "Onaylandı", Utc(14));
        return request;
    }

    private static DataSubjectRequestAssessmentResult Assessment(
        DataSubjectRequest request,
        bool canProceed,
        bool legalHold) =>
        DataSubjectRequestAssessmentResult.Record(new PersonalDataErasureAssessmentCompletedV1(
            Guid.NewGuid(), request.Id, request.RequesterUserId, "Coaching",
            canProceed, legalHold, 1, 0, 0, 0, 0, 0, Utc(14)));

    private static DateTime Utc(int hour) =>
        new(2026, 9, 21, hour, 0, 0, DateTimeKind.Utc);

    private sealed class StubRequestRepository(DataSubjectRequest request) : IDataSubjectRequestRepository
    {
        public int SaveCount { get; private set; }
        public Task<DataSubjectRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<DataSubjectRequest?>(id == request.Id ? request : null);
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
        public Task<bool> HasActiveAsync(Guid userId, DataSubjectRequestType requestType, PersonalDataScope scope, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(DataSubjectRequest value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<DataSubjectRequest>> GetByRequesterAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DataSubjectRequest>>([]);
        public Task<(IReadOnlyList<DataSubjectRequest> Items, int TotalCount)> GetForReviewAsync(DataSubjectRequestStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken) => Task.FromResult<(IReadOnlyList<DataSubjectRequest>, int)>(([request], 1));
    }

    private sealed class StubAssessmentRepository(params DataSubjectRequestAssessmentResult[] results)
        : IDataSubjectRequestAssessmentRepository
    {
        public Task RecordAsync(DataSubjectRequestAssessmentResult result, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<DataSubjectRequestAssessmentResult>> GetByRequestIdAsync(
            Guid requestId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DataSubjectRequestAssessmentResult>>(
                results.Where(result => result.RequestId == requestId).ToArray());
    }

    private sealed class StubExecutionPublisher : IDataSubjectRequestErasureExecutionPublisher
    {
        public List<PersonalDataErasureExecutionRequestedV1> Published { get; } = [];
        public Task PublishExecutionRequestedAsync(
            PersonalDataErasureExecutionRequestedV1 message,
            CancellationToken cancellationToken)
        {
            Published.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class SystemAdmin : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => ["SystemAdmin"];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
