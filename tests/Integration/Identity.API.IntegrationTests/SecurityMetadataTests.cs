using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Coaching.API.Controllers;
using EduPlatform.Shared.Security.Extensions;
using Identity.API.Controllers;
using Notification.API.Controllers;
using Xunit;

namespace Identity.API.IntegrationTests;

public class SecurityMetadataTests
{
    [Theory]
    [InlineData(typeof(AssignmentsController))]
    [InlineData(typeof(GoalsController))]
    [InlineData(typeof(SessionsController))]
    [InlineData(typeof(ExamsController))]
    public void CoachingControllers_MustRequireAuthorization(Type controllerType)
    {
        controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Should()
            .NotBeEmpty($"{controllerType.Name} exposes user data and must not be anonymous");
    }

    [Fact]
    public void SupportController_MustRequireAuthorization()
    {
        typeof(SupportController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Should()
            .NotBeEmpty("support reply is an administrative action");
    }

    [Fact]
    public void NotificationTestEndpoint_MustRequireAuthorization()
    {
        var action = typeof(NotificationsController).GetMethod(nameof(NotificationsController.GetAllNotifications));

        action.Should().NotBeNull();
        action!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Should()
            .NotBeEmpty("the test endpoint can return every user's notifications");
    }

    [Fact]
    public void InternalNotificationEndpoint_MustNotAllowAnonymousAccess()
    {
        var action = typeof(InternalNotificationController).GetMethod(nameof(InternalNotificationController.ForwardSupport));

        action.Should().NotBeNull();
        action!.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Should().BeEmpty();
        action.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Should().NotBeEmpty();
    }

    [Fact]
    public async Task CustomAuthorization_MustRequireAuthenticatedUsersByDefault()
    {
        var services = new ServiceCollection();
        services.AddCustomAuthorization();

        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var fallbackPolicy = await policyProvider.GetFallbackPolicyAsync();

        fallbackPolicy.Should().NotBeNull();
        fallbackPolicy!.Requirements.Should().ContainSingle(requirement =>
            requirement is DenyAnonymousAuthorizationRequirement);
    }
}
