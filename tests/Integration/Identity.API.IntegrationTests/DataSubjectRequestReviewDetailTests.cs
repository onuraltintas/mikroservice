using System.Security.Claims;
using EduPlatform.Shared.Contracts.Events.Privacy;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.DataSubjectRequests;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class DataSubjectRequestReviewDetailTests
{
    [Fact]
    public async Task Handle_ShouldReturnRequestWithAggregatedAssessment()
    {
        var request = DataSubjectRequest.Create(
            Guid.NewGuid(), DataSubjectRequestType.Erasure, PersonalDataScope.Coaching,
            "Silme talebi", Utc(12));
        var result = Result(request, "Coaching", canProceed: true, hasHold: false);
        var handler = new GetDataSubjectRequestReviewDetailQueryHandler(
            new StubRequestRepository(request),
            new StubAssessmentRepository(result),
            new StubCurrentUser());

        var detail = await handler.Handle(
            new GetDataSubjectRequestReviewDetailQuery(request.Id), CancellationToken.None);

        detail.Request.Id.Should().Be(request.Id);
        detail.Assessment.IsReadyForErasure.Should().BeTrue();
        detail.Assessment.Services.Should().ContainSingle(item => item.ServiceName == "Coaching");
    }

    private static DataSubjectRequestAssessmentResult Result(
        DataSubjectRequest request,
        string serviceName,
        bool canProceed,
        bool hasHold) =>
        DataSubjectRequestAssessmentResult.Record(
            new EduPlatform.Shared.Contracts.Events.Privacy.PersonalDataErasureAssessmentCompletedV1(
                Guid.NewGuid(), request.Id, request.RequesterUserId, serviceName,
                canProceed, hasHold, 1, 1, 1, 1, 1, 1, Utc(13)));

    private static DateTime Utc(int hour) =>
        new(2026, 9, 21, hour, 0, 0, DateTimeKind.Utc);

    private sealed class StubAssessmentRepository(params DataSubjectRequestAssessmentResult[] results)
        : IDataSubjectRequestAssessmentRepository
    {
        public Task RecordAsync(DataSubjectRequestAssessmentResult result, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<DataSubjectRequestAssessmentResult>> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DataSubjectRequestAssessmentResult>>(
                results.Where(result => result.RequestId == requestId).ToArray());
    }

    private sealed class StubRequestRepository(DataSubjectRequest request) : IDataSubjectRequestRepository
    {
        public Task<DataSubjectRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<DataSubjectRequest?>(request.Id == id ? request : null);
        public Task<bool> HasActiveAsync(Guid userId, DataSubjectRequestType requestType, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(DataSubjectRequest value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<DataSubjectRequest>> GetByRequesterAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DataSubjectRequest>>([]);
        public Task<(IReadOnlyList<DataSubjectRequest> Items, int TotalCount)> GetForReviewAsync(DataSubjectRequestStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken) => Task.FromResult<(IReadOnlyList<DataSubjectRequest>, int)>(([request], 1));
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubCurrentUser : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => ["SystemAdmin"];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
