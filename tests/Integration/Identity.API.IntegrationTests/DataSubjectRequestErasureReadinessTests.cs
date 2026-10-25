using EduPlatform.Shared.Contracts.Events.Privacy;
using Identity.Application.DataSubjectRequests;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class DataSubjectRequestErasureReadinessTests
{
    [Fact]
    public async Task Internal_readiness_query_returns_the_review_screen_aggregate_for_the_matching_request()
    {
        var now = DateTime.UtcNow;
        var subjectUserId = Guid.NewGuid();
        var request = DataSubjectRequest.Create(
            subjectUserId, DataSubjectRequestType.Erasure, PersonalDataScope.Account,
            "Account erasure", now.AddMinutes(-3));
        request.VerifyIdentity(now.AddMinutes(-2));
        request.Approve(Guid.NewGuid(), "Approved", now.AddMinutes(-1));
        request.StartProcessing(now);
        var results = new[] { "Coaching", "Notification", "SpeedReading", "CourseManagement" }
            .Select(serviceName => DataSubjectRequestAssessmentResult.Record(
                new PersonalDataErasureAssessmentCompletedV1(
                    Guid.NewGuid(), request.Id, subjectUserId, serviceName,
                    CanProceed: true, HasActiveLegalHold: false,
                    AssignmentCount: 0, AttachmentCount: 0, ExamResultCount: 0,
                    GoalCount: 0, SessionCount: 0, AgreementCount: 0, now)))
            .ToArray();
        var handler = new GetDataSubjectRequestErasureReadinessQueryHandler(
            new RequestRepository(request), new AssessmentRepository(results));

        var result = await handler.Handle(
            new GetDataSubjectRequestErasureReadinessQuery(request.Id), CancellationToken.None);

        Assert.Equal(request.Id, result.RequestId);
        Assert.Equal(subjectUserId, result.SubjectUserId);
        Assert.Equal("Erasure", result.RequestType);
        Assert.Equal("Account", result.Scope);
        Assert.Equal("Processing", result.Status);
        Assert.True(result.AssessmentIsComplete);
        Assert.False(result.HasBlockingLegalHold);
        Assert.True(result.IsReadyForErasure);
    }

    [Fact]
    public void Internal_readiness_endpoint_requires_internal_service_authentication()
    {
        var method = typeof(Identity.API.Controllers.InternalDataSubjectRequestsController)
            .GetMethod("GetErasureReadiness")!;

        Assert.NotEmpty(method.GetCustomAttributes(typeof(Identity.API.InternalServiceKeyAttribute), true));
        Assert.NotEmpty(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true));
    }

    private sealed class RequestRepository(DataSubjectRequest request) : IDataSubjectRequestRepository
    {
        public Task<DataSubjectRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(id == request.Id ? request : null);

        public Task<bool> HasActiveAsync(Guid userId, DataSubjectRequestType requestType, PersonalDataScope scope,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddAsync(DataSubjectRequest value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DataSubjectRequest>> GetByRequesterAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<(IReadOnlyList<DataSubjectRequest> Items, int TotalCount)> GetForReviewAsync(
            DataSubjectRequestStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class AssessmentRepository(IReadOnlyList<DataSubjectRequestAssessmentResult> results)
        : IDataSubjectRequestAssessmentRepository
    {
        public Task<IReadOnlyList<DataSubjectRequestAssessmentResult>> GetByRequestIdAsync(
            Guid requestId, CancellationToken cancellationToken) => Task.FromResult(results);
        public Task RecordAsync(DataSubjectRequestAssessmentResult result, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
