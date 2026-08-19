using System.Text.Json;
using EduPlatform.Shared.Infrastructure.Middleware;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task ValidationException_ShouldReturnProblemDetailsWithCorrelationMetadata()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/users";
        context.TraceIdentifier = "trace-test-123";
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var exception = new ValidationException(new Dictionary<string, string[]>
        {
            ["Email"] = ["Email is required."]
        });

        var handled = await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        responseBody.Position = 0;
        using var document = await JsonDocument.ParseAsync(responseBody);
        var root = document.RootElement;
        root.GetProperty("title").GetString().Should().Be("Validation Error");
        root.GetProperty("type").GetString()
            .Should().Be("https://eduplatform.dev/problems/validation-error");
        root.GetProperty("instance").GetString().Should().Be("/api/users");
        root.GetProperty("traceId").GetString().Should().Be("trace-test-123");
        root.GetProperty("errors").GetProperty("Email")[0].GetString()
            .Should().Be("Email is required.");
        root.TryGetProperty("stackTrace", out _).Should().BeFalse();
    }
}
