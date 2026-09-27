using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using SpeedReading.API.Controllers;

namespace Identity.API.IntegrationTests;

public sealed class SpeedReadingAdminErasureEndpointTests
{
    [Fact]
    public void DeleteStudentData_RequiresSystemAdminAndPrivacyManage()
    {
        var method = typeof(StudentProgressAdminController).GetMethod("DeleteStudentData");
        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Should().Contain(attribute => attribute.Roles == "SystemAdmin")
            .And.Contain(attribute => attribute.Policy == "MfaRequired");
        method.GetCustomAttributes(typeof(HasPermissionAttribute), true)
            .Cast<HasPermissionAttribute>()
            .Should().NotBeEmpty();
    }
}
