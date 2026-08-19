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
    public async Task AdminMutation_ShouldWriteMetadataOnlyAuditRecord()
    {
        var writer = new CapturingAuditWriter();
        var context = CreateContext("POST", "/api/users/target-id", "SystemAdmin");
        context.Request.Body = new MemoryStream("secret-password"u8.ToArray());
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
        record.GetType().GetProperties()
            .Select(property => property.GetValue(record)?.ToString())
            .Should().NotContain(value => value?.Contains("secret-password") == true);
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
