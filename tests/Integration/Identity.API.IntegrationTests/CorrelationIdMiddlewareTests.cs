using EduPlatform.Shared.Infrastructure.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Identity.API.IntegrationTests;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task MissingCorrelationId_ShouldGenerateAndReturnSafeId()
    {
        var context = new DefaultHttpContext();
        var next = new RequestDelegate(httpContext =>
        {
            httpContext.TraceIdentifier.Should().NotBeNullOrWhiteSpace();
            return Task.CompletedTask;
        });

        await new CorrelationIdMiddleware(next).InvokeAsync(context);

        var correlationId = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        correlationId.Should().MatchRegex("^[0-9a-f]{32}$");
        context.TraceIdentifier.Should().Be(correlationId);
    }

    [Fact]
    public async Task ValidCorrelationId_ShouldBePreservedAcrossRequestAndResponse()
    {
        const string correlationId = "edge-2026-08-14-001";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;

        await new CorrelationIdMiddleware(_ => Task.CompletedTask).InvokeAsync(context);

        context.TraceIdentifier.Should().Be(correlationId);
        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString()
            .Should().Be(correlationId);
    }

    [Theory]
    [InlineData("bad\r\nid")]
    [InlineData("contains spaces")]
    [InlineData("<script>")]
    public async Task UnsafeCorrelationId_ShouldBeReplaced(string unsafeCorrelationId)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = unsafeCorrelationId;

        await new CorrelationIdMiddleware(_ => Task.CompletedTask).InvokeAsync(context);

        var correlationId = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        correlationId.Should().MatchRegex("^[0-9a-f]{32}$");
    }
}
