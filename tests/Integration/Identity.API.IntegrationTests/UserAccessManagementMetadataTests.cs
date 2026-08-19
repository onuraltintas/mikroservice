using System.Reflection;
using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Identity.API.Controllers;
using Identity.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Identity.API.IntegrationTests;

public sealed class UserAccessManagementMetadataTests
{
    public static TheoryData<string> CriticalAccessActions => new()
    {
        nameof(UserController.GetSessions),
        nameof(UserController.RevokeSession),
        nameof(UserController.RevokeAllSessions),
        nameof(UserController.ResetMfa)
    };

    [Theory]
    [MemberData(nameof(CriticalAccessActions))]
    public void AccessManagementAction_ShouldRequireSystemAdminMfaAndEditPermission(string actionName)
    {
        var method = typeof(UserController).GetMethod(actionName)
            ?? throw new InvalidOperationException($"Missing action {actionName}.");
        var authorize = method.GetCustomAttributes<AuthorizeAttribute>().ToArray();
        var permission = method.GetCustomAttribute<HasPermissionAttribute>();

        authorize.Should().Contain(attribute => attribute.Roles == "SystemAdmin");
        authorize.Should().Contain(attribute => attribute.Policy == "MfaRequired");
        permission.Should().NotBeNull();
        permission!.Policy.Should().Be(Permissions.Users.Edit);
    }
}
