using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using SpeedReading.API.Controllers;

namespace Identity.API.IntegrationTests;

public sealed class SpeedReadingAdminAuditMetadataTests
{
    [Fact]
    public void AdminAuditController_MustRequireSystemAdminAndOperationsPermission()
    {
        var controller = typeof(AdminAuditController);

        controller.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .Contain(attribute => attribute.Roles == "SystemAdmin");
        controller.GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
            .Cast<HasPermissionAttribute>()
            .Should()
            .Contain(attribute => attribute.Policy == PlatformPermissions.Operations.View);
    }

    [Fact]
    public void AdminAuditController_MustExposeRecordsAndFacetsEndpoints()
    {
        typeof(AdminAuditController).GetMethod(nameof(AdminAuditController.GetAsync)).Should().NotBeNull();
        typeof(AdminAuditController).GetMethod(nameof(AdminAuditController.GetFacetsAsync)).Should().NotBeNull();
    }
}
