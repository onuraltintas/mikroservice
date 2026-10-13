using Coaching.API.Controllers;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.API.IntegrationTests;

public sealed class CoachingFeeApiSurfaceTests
{
    [Fact]
    public void FinancialWritesRequireStaffRoleMfaAndBoundedRateLimitedBodies()
    {
        foreach (var name in new[] { nameof(CoachingFeesController.Create), nameof(CoachingFeesController.Pay), nameof(CoachingFeesController.Reverse), nameof(CoachingFeesController.Cancel) }) {
            var method = typeof(CoachingFeesController).GetMethod(name)!;
            var roles = Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Roles!;
            Assert.Contains("InstitutionOwner", roles); Assert.Contains("Teacher", roles); Assert.DoesNotContain("Student", roles); Assert.DoesNotContain("SystemAdmin", roles);
            Assert.Single(method.GetCustomAttributes(typeof(MfaCategoryAttribute), false));
            Assert.Single(method.GetCustomAttributes(typeof(RequestSizeLimitAttribute), false));
            Assert.Single(method.GetCustomAttributes(typeof(EnableRateLimitingAttribute), false));
        }
        Assert.Equal("Student", typeof(CoachingFeesController).GetMethod(nameof(CoachingFeesController.MyAccount))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().Single().Roles);
    }

    [Fact]
    public void AdminFinancialReviewIsReadOnlyAndRequiresPermissionAndMfa()
    {
        var controller = typeof(CoachingAdminFeesController);
        Assert.Contains("SystemAdmin", controller.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().Single().Roles!);
        Assert.Single(controller.GetCustomAttributes(typeof(HasPermissionAttribute), false));
        Assert.Single(controller.GetCustomAttributes(typeof(MfaCategoryAttribute), false));
        Assert.DoesNotContain(controller.GetMethods(), method => method.GetCustomAttributes(typeof(HttpPostAttribute), false).Length > 0);
    }

    [Theory]
    [InlineData("/api/coaching/finance/me")]
    [InlineData("/api/coaching/finance/me/fees/00000000-0000-0000-0000-000000000001/payments")]
    public void OwnFinancialRecordsRemainAvailableWithoutAnActiveLearningSubscription(string path) =>
        Assert.False(Coaching.Application.Subscriptions.CoachingSubscriptionAccessRules.RequiresSubscription(path));
}
