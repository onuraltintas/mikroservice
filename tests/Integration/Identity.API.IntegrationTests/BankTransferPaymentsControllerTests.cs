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
    public void DeleteRequest_ShouldRequireSubscriptionManagePermission()
    {
        var action = typeof(BankTransferPaymentsController).GetMethod("DeleteRequest");

        action.Should().NotBeNull();
        action!.GetCustomAttribute<HttpDeleteAttribute>()!.Template
            .Should().Be("requests/{id:guid}");
        action.GetCustomAttributes<AuthorizeAttribute>().Should().NotBeEmpty();
        action.GetCustomAttribute<HasPermissionAttribute>()!.Permission
            .Should().Be(PlatformPermissions.SpeedReading.SubscriptionManage);
    }

    [Theory]
    [InlineData(typeof(BankTransferPaymentsController))]
    [InlineData(typeof(PaymentsController))]
    [InlineData(typeof(SubscriptionsController))]
    [InlineData(typeof(SubscriptionPlansController))]
    [InlineData(typeof(SubscriptionProductsController))]
    public void Money_administration_needs_its_own_permission_not_content_editing(Type controller)
    {
        // Review H3 (finding 11): approving transfers, changing the IBAN, prices or granting access is money work; a
        // content editor must not be able to do it. Same split as coaching (SubscriptionManage).
        var permissions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(action => action.GetCustomAttributes<HasPermissionAttribute>())
            .Concat(controller.GetCustomAttributes<HasPermissionAttribute>())
            .Select(attribute => attribute.Permission)
            .ToList();

        permissions.Should().NotBeEmpty();
        permissions.Should().OnlyContain(permission => permission == PlatformPermissions.SpeedReading.SubscriptionManage);
        PlatformPermissions.GetAll().Should().Contain(PlatformPermissions.SpeedReading.SubscriptionManage);
    }
}
