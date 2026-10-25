using System.Net;
using System.Net.Http.Json;
using CourseManagement.Domain;
using CourseManagement.Infrastructure.Privacy;
using Microsoft.Extensions.Configuration;

namespace CourseManagement.Application.UnitTests;

public sealed class IdentityCourseManagementErasureReadinessProviderTests
{
    [Fact]
    public async Task Ready_identity_review_summary_allows_erasure_and_uses_internal_service_key()
    {
        var requestId = Guid.NewGuid();
        var subjectUserId = Guid.NewGuid();
        using var http = new HttpClient(new StubHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                $"http://identity.test/api/internal/data-subject-requests/{requestId}/erasure-readiness",
                request.RequestUri!.ToString());
            Assert.Equal("local-test-key-0123456789abcdef0", Assert.Single(request.Headers.GetValues("X-Internal-Service-Key")));
            return Task.FromResult(JsonResponse(new
            {
                requestId,
                subjectUserId,
                requestType = "Erasure",
                scope = "Account",
                status = "Processing",
                assessmentIsComplete = true,
                hasBlockingLegalHold = false,
                isReadyForErasure = true
            }));
        }));
        var provider = CreateProvider(http);

        var status = await provider.GetStatusAsync(requestId, subjectUserId, CancellationToken.None);

        Assert.Equal(CourseManagementLegalHoldStatus.NoActiveHold, status);
    }

    [Fact]
    public async Task Blocking_hold_in_identity_review_summary_blocks_erasure()
    {
        var requestId = Guid.NewGuid();
        var subjectUserId = Guid.NewGuid();
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(JsonResponse(new
        {
            requestId,
            subjectUserId,
            requestType = "Erasure",
            scope = "Account",
            status = "Processing",
            assessmentIsComplete = true,
            hasBlockingLegalHold = true,
            isReadyForErasure = false
        }))));
        var provider = CreateProvider(http);

        var status = await provider.GetStatusAsync(requestId, subjectUserId, CancellationToken.None);

        Assert.Equal(CourseManagementLegalHoldStatus.ActiveHold, status);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Identity_dependency_failure_fails_closed(HttpStatusCode responseStatus)
    {
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(responseStatus))));
        var provider = CreateProvider(http);

        var status = await provider.GetStatusAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(CourseManagementLegalHoldStatus.Unknown, status);
    }

    [Fact]
    public async Task Incomplete_or_mismatched_identity_summary_fails_closed()
    {
        var requestId = Guid.NewGuid();
        var subjectUserId = Guid.NewGuid();
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(JsonResponse(new
        {
            requestId,
            subjectUserId = Guid.NewGuid(),
            requestType = "Erasure",
            scope = "Account",
            status = "Processing",
            assessmentIsComplete = false,
            hasBlockingLegalHold = false,
            isReadyForErasure = false
        }))));
        var provider = CreateProvider(http);

        var status = await provider.GetStatusAsync(requestId, subjectUserId, CancellationToken.None);

        Assert.Equal(CourseManagementLegalHoldStatus.Unknown, status);
    }

    private static IdentityCourseManagementErasureReadinessProvider CreateProvider(HttpClient http) =>
        new(http, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Services:IdentityService"] = "http://identity.test",
            ["INTERNAL_SERVICE_API_KEY"] = "local-test-key-0123456789abcdef0"
        }).Build());

    private static HttpResponseMessage JsonResponse<T>(T payload) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(payload) };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request);
    }
}
