using System.Security.Claims;
using EduPlatform.Shared.Infrastructure.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class AdminAuditMiddlewareTests
{
    [Fact]
    public async Task AdminMutation_ShouldWriteSafeChangeMetadataWithoutPayloadValues()
    {
        var writer = new CapturingAuditWriter();
        var context = CreateContext("POST", "/api/users/target-id", "SystemAdmin");
        context.Request.ContentType = "application/json";
        var body = "{\"title\":\"New title\",\"password\":\"secret-password\"}"u8.ToArray();
        context.Request.ContentLength = body.Length;
        context.Request.Body = new MemoryStream(body);
        var middleware = new AdminAuditMiddleware(
            next: httpContext =>
            {
                httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            },
            new TestHostEnvironment(),
            NullLogger<AdminAuditMiddleware>.Instance);

        await middleware.InvokeAsync(context, writer);

        writer.Records.Should().ContainSingle();
        var record = writer.Records.Single();
        record.ActorUserId.Should().Be("actor-id");
        record.HttpMethod.Should().Be("POST");
        record.Path.Should().Be("/api/users/target-id");
        record.StatusCode.Should().Be(204);
        record.CorrelationId.Should().Be(context.TraceIdentifier);
        record.ServiceName.Should().Be("Identity.API.Tests");
        record.Action.Should().Be("create");
        record.ResourceType.Should().Be("users");
        record.ResourceId.Should().Be("target-id");
        record.ChangedFieldsJson.Should().Contain("title");
        record.ChangedFieldsJson.Should().NotContain("password");
        record.ChangedFieldsJson.Should().NotContain("secret-password");
    }

    [Fact]
    public async Task AdminEdit_ShouldWriteAuditRecord()
    {
        var writer = new CapturingAuditWriter();
        var context = CreateContext("PUT", "/api/coaching-admin/assignments/assignment-id", "SystemAdmin");
        var middleware = new AdminAuditMiddleware(
            _ => Task.CompletedTask,
            new TestHostEnvironment(),
            NullLogger<AdminAuditMiddleware>.Instance);

        await middleware.InvokeAsync(context, writer);

        writer.Records.Should().ContainSingle();
        writer.Records.Single().HttpMethod.Should().Be("PUT");
        writer.Records.Single().Path.Should().Be("/api/coaching-admin/assignments/assignment-id");
        writer.Records.Single().Action.Should().Be("update");
        writer.Records.Single().ResourceType.Should().Be("assignments");
        writer.Records.Single().ResourceId.Should().Be("assignment-id");
    }

    [Fact]
    public async Task AdminOperation_ShouldUseRouteOperationAsAuditAction()
    {
        var writer = new CapturingAuditWriter();
        var middleware = new AdminAuditMiddleware(
            _ => Task.CompletedTask,
            new TestHostEnvironment(),
            NullLogger<AdminAuditMiddleware>.Instance);

        await middleware.InvokeAsync(
            CreateContext("POST", "/api/coaching-admin/assignments/assignment-id/cancel", "SystemAdmin"),
            writer);

        writer.Records.Should().ContainSingle();
        writer.Records.Single().Action.Should().Be("cancel");
        writer.Records.Single().ResourceType.Should().Be("assignments");
        writer.Records.Single().ResourceId.Should().Be("assignment-id");
    }

    [Theory]
    [InlineData("GET", "SystemAdmin")]
    [InlineData("POST", "Student")]
    public async Task ReadOrNonAdminRequest_ShouldNotCreateAdminAuditRecord(
        string method,
        string role)
    {
        var writer = new CapturingAuditWriter();
        var middleware = new AdminAuditMiddleware(
            _ => Task.CompletedTask,
            new TestHostEnvironment(),
            NullLogger<AdminAuditMiddleware>.Instance);

        await middleware.InvokeAsync(CreateContext(method, "/api/users", role), writer);

        writer.Records.Should().BeEmpty();
    }

    private static DefaultHttpContext CreateContext(string method, string path, string role)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "actor-id"),
                new Claim(ClaimTypes.Role, role),
                new Claim("institution_id", "tenant-id")
            ], "test"))
        };
        context.Request.Method = method;
        context.Request.Path = path;
        return context;
    }

    private sealed class CapturingAuditWriter : IAdminAuditWriter
    {
        public List<AdminAuditRecord> Records { get; } = [];

        public Task WriteAsync(AdminAuditRecord record, CancellationToken cancellationToken)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Identity.API.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
