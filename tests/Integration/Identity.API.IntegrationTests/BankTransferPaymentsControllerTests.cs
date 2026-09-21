using System.Reflection;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpeedReading.API.Controllers;
using Xunit;

namespace Identity.API.IntegrationTests;

public sealed class BankTransferPaymentsControllerTests
{
    [Fact]
    public void CreateRequest_ShouldUseDedicatedRateLimit()
    {
        var action = typeof(BankTransferPaymentsController).GetMethod(nameof(BankTransferPaymentsController.CreateRequest));

        var rateLimit = action!.GetCustomAttribute<EnableRateLimitingAttribute>();

        rateLimit.Should().NotBeNull();
        rateLimit!.PolicyName.Should().Be("payment-request");
    }

    [Fact]
    public void DeleteRequest_ShouldRequireContentManagePermission()
    {
        var action = typeof(BankTransferPaymentsController).GetMethod("DeleteRequest");

        action.Should().NotBeNull();
        action!.GetCustomAttribute<HttpDeleteAttribute>()!.Template
            .Should().Be("requests/{id:guid}");
        action.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        action.GetCustomAttribute<HasPermissionAttribute>()!.Permission
            .Should().Be(PlatformPermissions.SpeedReading.ContentManage);
    }
}
