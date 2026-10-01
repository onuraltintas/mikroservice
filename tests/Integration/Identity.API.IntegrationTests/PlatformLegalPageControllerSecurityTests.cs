using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Identity.API.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Identity.API.IntegrationTests;

public sealed class PlatformLegalPageControllerSecurityTests
{
    [Fact]
    public void AdminLegalPageEndpointsRequireSystemAdminMfaAndPrivacyManagePermission()
    {
        var attributes = typeof(PlatformLegalPagesAdminController)
            .GetCustomAttributes(inherit: true)
            .ToArray();

        attributes.OfType<AuthorizeAttribute>().Should().Contain(attribute => attribute.Roles == "SystemAdmin");
        attributes.OfType<AuthorizeAttribute>().Should().Contain(attribute => attribute.Policy == "MfaRequired");
        attributes.OfType<HasPermissionAttribute>().Should().Contain(attribute =>
            attribute.Policy == PlatformPermissions.Privacy.Manage);
    }

    [Fact]
    public void PublicLegalPageEndpointIsAnonymousButDoesNotExposeAdminController()
    {
        typeof(PlatformLegalPagesController).GetCustomAttributes(inherit: true)
            .OfType<AllowAnonymousAttribute>().Should().ContainSingle();
        typeof(PlatformLegalPagesAdminController).GetCustomAttributes(inherit: true)
            .OfType<AllowAnonymousAttribute>().Should().BeEmpty();
    }
}
