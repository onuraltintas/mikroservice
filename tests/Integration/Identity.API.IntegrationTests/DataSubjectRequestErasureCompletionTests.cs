using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using Identity.Application.DataSubjectRequests;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class DataSubjectRequestErasureCompletionTests
{
    [Fact]
    public async Task Completion_ShouldFinishCoachingScopedRequestIdempotently()
    {
        var request = ProcessingRequest(PersonalDataScope.Coaching);
        var requestRepository = new StubRequestRepository(request);
        var executionRepository = new StubExecutionRepository();
        var handler = new DataSubjectRequestErasureCompletionHandler(
            requestRepository, executionRepository);
        var message = Completed(request.Id, "Coaching");

        await handler.HandleAsync(message, CancellationToken.None);
        await handler.HandleAsync(message, CancellationToken.None);

        request.Status.Should().Be(DataSubjectRequestStatus.Completed);
        executionRepository.Results.Should().ContainSingle();
        requestRepository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Completion_ShouldWaitForEveryAccountService()
    {
        var request = ProcessingRequest(PersonalDataScope.Account);
        var handler = new DataSubjectRequestErasureCompletionHandler(
            new StubRequestRepository(request),
            new StubExecutionRepository());

        await handler.HandleAsync(Completed(request.Id, "Coaching"), CancellationToken.None);
        request.Status.Should().Be(DataSubjectRequestStatus.Processing);
        await handler.HandleAsync(Completed(request.Id, "Notification"), CancellationToken.None);
        request.Status.Should().Be(DataSubjectRequestStatus.Processing);
        await handler.HandleAsync(Completed(request.Id, "SpeedReading"), CancellationToken.None);

        request.Status.Should().Be(DataSubjectRequestStatus.Completed);
    }

    [Fact]
    public async Task Completion_ShouldRejectServiceOutsideRequestScope()
    {
        var request = ProcessingRequest(PersonalDataScope.Coaching);
        var handler = new DataSubjectRequestErasureCompletionHandler(
            new StubRequestRepository(request),
            new StubExecutionRepository());

        var action = () => handler.HandleAsync(
            Completed(request.Id, "SpeedReading"), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*scope*");
        request.Status.Should().Be(DataSubjectRequestStatus.Processing);
    }

    private static DataSubjectRequest ProcessingRequest(PersonalDataScope scope)
    {
        var request = DataSubjectRequest.Create(
            Guid.NewGuid(), DataSubjectRequestType.Erasure, scope, "Silme", Utc(10));
        request.VerifyIdentity(Utc(11));
        request.Approve(Guid.NewGuid(), "Onay", Utc(12));
        request.StartProcessing(Utc(13));
        return request;
    }

    private static PersonalDataErasureExecutionCompletedV1 Completed(Guid requestId, string service) =>
        new(Guid.NewGuid(), requestId, service, 2, Utc(14));

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

    private sealed class StubExecutionRepository : IDataSubjectRequestExecutionRepository
    {
        public List<DataSubjectRequestExecutionResult> Results { get; } = [];

        public Task<bool> ExistsAsync(Guid requestId, string serviceName, CancellationToken cancellationToken) =>
            Task.FromResult(Results.Any(result => result.RequestId == requestId && result.ServiceName == serviceName));

        public Task AddAsync(DataSubjectRequestExecutionResult result, CancellationToken cancellationToken)
        {
            Results.Add(result);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DataSubjectRequestExecutionResult>> GetByRequestIdAsync(
            Guid requestId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DataSubjectRequestExecutionResult>>(
                Results.Where(result => result.RequestId == requestId).ToArray());
    }
}
