using FluentAssertions;
using Identity.API.Controllers.Settings;
using Identity.Application.DTOs.Logs;
using Identity.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Notification.API.Controllers;

namespace Identity.API.IntegrationTests;

public sealed class AdminSurfaceSecurityTests
{
    [Fact]
    public void NotificationApi_ShouldNotExposeTestOnlyGlobalNotificationEndpoint()
    {
        var exposedTemplates = typeof(NotificationsController)
            .GetMethods()
            .SelectMany(method => method.GetCustomAttributes(typeof(HttpGetAttribute), inherit: true))
            .Cast<HttpGetAttribute>()
            .Select(attribute => attribute.Template);

        exposedTemplates.Should().NotContain("test-all");
    }

    [Fact]
    public async Task SystemLogFailure_ShouldFlowToGlobalProblemDetailsHandler()
    {
        var controller = new SystemLogsController(
            new ThrowingSystemLogService(),
            NullLogger<SystemLogsController>.Instance);

        var action = () => controller.GetLogs(new LogFilterRequest(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("internal-provider-detail");
    }

    [Fact]
    public void SeqUrl_ShouldComeFromConfiguredLogService()
    {
        var controller = new SystemLogsController(
            new ConfiguredSystemLogService(),
            NullLogger<SystemLogsController>.Instance);

        var result = controller.GetSeqUrl();

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new { Url = "https://logs.example.test" });
    }

    private sealed class ThrowingSystemLogService : ISystemLogService
    {
        private static InvalidOperationException Failure() => new("internal-provider-detail");
        public Task<PagedLogsResponse> GetLogsAsync(LogFilterRequest request, CancellationToken cancellationToken) => throw Failure();
        public Task<List<string>> GetApplicationsAsync(CancellationToken cancellationToken) => throw Failure();
        public Task<List<RetentionPolicyDto>> GetRetentionPoliciesAsync(CancellationToken cancellationToken) => throw Failure();
        public Task<RetentionPolicyDto?> CreateRetentionPolicyAsync(CreateRetentionPolicyRequest request, CancellationToken cancellationToken) => throw Failure();
        public Task<bool> DeleteRetentionPolicyAsync(string policyId, CancellationToken cancellationToken) => throw Failure();
        public string GetSeqUrl() => throw Failure();
    }

    private sealed class ConfiguredSystemLogService : ISystemLogService
    {
        public Task<PagedLogsResponse> GetLogsAsync(LogFilterRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<List<string>> GetApplicationsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<List<RetentionPolicyDto>> GetRetentionPoliciesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RetentionPolicyDto?> CreateRetentionPolicyAsync(CreateRetentionPolicyRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DeleteRetentionPolicyAsync(string policyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public string GetSeqUrl() => "https://logs.example.test";
    }
}
