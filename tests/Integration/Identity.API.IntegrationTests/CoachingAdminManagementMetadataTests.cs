using Coaching.API.Controllers;
using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAdminManagementMetadataTests
{
    [Fact]
    public void CoachingAdminOverview_MustRequireSystemAdminAndPermission()
    {
        var authorizeAttributes = typeof(CoachingAdminController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>();

        authorizeAttributes.Should().Contain(attribute => attribute.Roles == "SystemAdmin");

        typeof(CoachingAdminController)
            .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
            .Cast<HasPermissionAttribute>()
            .Should()
            .Contain(attribute => attribute.Policy == "Permissions.Coaching.View");
    }
}
