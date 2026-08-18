using System.Net;
using System.Net.Http;
using EduPlatform.Shared.Infrastructure.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Identity.API.IntegrationTests;

public sealed class CorrelationIdDelegatingHandlerTests
{
    [Fact]
    public async Task SendAsync_ShouldPropagateCurrentCorrelationId()
    {
        const string correlationId = "gateway-2026-08-18-001";
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                TraceIdentifier = correlationId
            }
        };
        var capture = new CapturingHandler();
        using var client = CreateClient(accessor, capture);

        await client.GetAsync("http://identity/internal");

        capture.Request.Should().NotBeNull();
        capture.Request!.Headers.GetValues(CorrelationIdMiddleware.HeaderName)
            .Should().ContainSingle().Which.Should().Be(correlationId);
    }

    [Fact]
    public async Task SendAsync_ShouldNotOverwriteExplicitCorrelationId()
    {
        const string explicitCorrelationId = "explicit-2026-08-18";
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                TraceIdentifier = "gateway-2026-08-18-001"
            }
        };
        var capture = new CapturingHandler();
        using var client = CreateClient(accessor, capture);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://identity/internal");
        request.Headers.TryAddWithoutValidation(
            CorrelationIdMiddleware.HeaderName,
            explicitCorrelationId);

        await client.SendAsync(request);

        capture.Request!.Headers.GetValues(CorrelationIdMiddleware.HeaderName)
            .Should().ContainSingle().Which.Should().Be(explicitCorrelationId);
    }

    [Fact]
    public async Task SendAsync_ShouldReplaceUnsafeExplicitCorrelationId()
    {
        const string currentCorrelationId = "gateway-2026-08-18-001";
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                TraceIdentifier = currentCorrelationId
            }
        };
        var capture = new CapturingHandler();
        using var client = CreateClient(accessor, capture);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://identity/internal");
        request.Headers.TryAddWithoutValidation(
            CorrelationIdMiddleware.HeaderName,
            "<script>");

        await client.SendAsync(request);

        capture.Request!.Headers.GetValues(CorrelationIdMiddleware.HeaderName)
            .Should().ContainSingle().Which.Should().Be(currentCorrelationId);
    }

    [Fact]
    public async Task SendAsync_WithoutHttpContext_ShouldNotAddCorrelationId()
    {
        var capture = new CapturingHandler();
        using var client = CreateClient(new HttpContextAccessor(), capture);

        await client.GetAsync("http://identity/internal");

        capture.Request.Should().NotBeNull();
        capture.Request!.Headers.Contains(CorrelationIdMiddleware.HeaderName).Should().BeFalse();
    }

    private static HttpClient CreateClient(
        IHttpContextAccessor accessor,
        CapturingHandler capture)
    {
        var handler = new CorrelationIdDelegatingHandler(accessor)
        {
            InnerHandler = capture
        };
        return new HttpClient(handler);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
