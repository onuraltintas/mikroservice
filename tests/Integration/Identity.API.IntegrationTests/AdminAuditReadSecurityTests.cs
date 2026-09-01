using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class AdminAuditReadSecurityTests
{
    public static TheoryData<System.Reflection.Assembly, string, string> Controllers => new()
    {
        {
            typeof(Identity.API.Controllers.AuthController).Assembly,
            "Identity.API.Controllers.AdminAuditController",
            "api/admin-audit/identity"
        },
        {
            typeof(Notification.API.Controllers.NotificationsController).Assembly,
            "Notification.API.Controllers.AdminAuditController",
            "api/admin-audit/notification"
        },
        {
            typeof(Coaching.API.Controllers.AssignmentsController).Assembly,
            "Coaching.API.Controllers.AdminAuditController",
            "api/admin-audit/coaching"
        },
        {
            typeof(SpeedReading.API.Controllers.AdminAnalyticsController).Assembly,
            "SpeedReading.API.Controllers.AdminAuditController",
            "api/admin-audit/speed-reading"
        }
    };

    [Theory]
    [MemberData(nameof(Controllers))]
    public void AuditReadController_ShouldRequireSystemAdminAndOperationsPermission(
        System.Reflection.Assembly assembly,
        string typeName,
        string expectedRoute)
    {
        var controller = assembly.GetType(typeName);

        controller.Should().NotBeNull();
        controller!.GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Should().ContainSingle(attribute => attribute.Template == expectedRoute);
        controller.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should().Contain(attribute => attribute.Roles == "SystemAdmin");
        controller.GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
            .Cast<HasPermissionAttribute>()
            .Should().Contain(attribute =>
                attribute.Policy == PlatformPermissions.Operations.View);
    }
}
