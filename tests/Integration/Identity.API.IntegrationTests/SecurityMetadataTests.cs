using FluentAssertions;
using EduPlatform.Shared.Infrastructure.Extensions;
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
    public void InternalNotificationEndpoint_MustUseServiceAuthentication()
    {
        var action = typeof(InternalNotificationController).GetMethod(nameof(InternalNotificationController.ForwardSupport));

        action.Should().NotBeNull();
        action!.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Should().NotBeEmpty();
        action.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Should().BeEmpty();
    }

    [Fact]
    public void PublicSupportSubmit_MustAllowAnonymousAccess()
    {
        var action = typeof(SupportController).GetMethod(nameof(SupportController.Submit));

        action.Should().NotBeNull();
        action!.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Should().NotBeEmpty();
    }

    [Fact]
    public void SupportReply_MustRemainAuthenticated()
    {
        var action = typeof(SupportController).GetMethod(nameof(SupportController.Reply));

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

    [Fact]
    public async Task PermissionPolicies_MustRequireAuthenticatedUsers()
    {
        var services = new ServiceCollection();
        services.AddCustomAuthorization();

        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync("Permissions.Users.View");

        policy.Should().NotBeNull();
        policy!.Requirements.Should().Contain(requirement =>
            requirement is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public void SharedMediatorBehaviors_MustUseMediatRPipeline()
    {
        var services = new ServiceCollection();
        services.AddMediatorWithBehaviors();

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(MediatR.IPipelineBehavior<,>));
    }
}
