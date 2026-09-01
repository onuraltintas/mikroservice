using FluentAssertions;
using Identity.API.Controllers;
using Identity.API.Controllers.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using EduPlatform.Shared.Security.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

public sealed class SystemAdminStepUpPolicyTests
{
    public static TheoryData<Type, string> CriticalActions => new()
    {
        { typeof(UserController), nameof(UserController.CreateUser) },
        { typeof(UserController), nameof(UserController.DeleteUser) },
        { typeof(UserController), nameof(UserController.ActivateUser) },
        { typeof(UserController), nameof(UserController.ConfirmEmail) },
        { typeof(UserController), nameof(UserController.ChangePassword) },
        { typeof(UserController), nameof(UserController.UpdateUser) },
        { typeof(UserController), nameof(UserController.UpdateUserProfile) },
        { typeof(UserController), nameof(UserController.AssignRole) },
        { typeof(UserController), nameof(UserController.RemoveRole) },
        { typeof(RolesController), nameof(RolesController.CreateRole) },
        { typeof(RolesController), nameof(RolesController.UpdateRole) },
        { typeof(RolesController), nameof(RolesController.DeleteRole) },
        { typeof(RolesController), nameof(RolesController.RestoreRole) },
        { typeof(RolesController), nameof(RolesController.UpdateRolePermissions) },
        { typeof(PermissionsController), nameof(PermissionsController.CreatePermission) },
        { typeof(PermissionsController), nameof(PermissionsController.UpdatePermission) },
        { typeof(PermissionsController), nameof(PermissionsController.DeletePermission) },
        { typeof(PermissionsController), nameof(PermissionsController.RestorePermission) },
        { typeof(ConfigurationsController), nameof(ConfigurationsController.Create) },
        { typeof(ConfigurationsController), nameof(ConfigurationsController.Update) },
        { typeof(ConfigurationsController), nameof(ConfigurationsController.Delete) },
        { typeof(ConfigurationsController), nameof(ConfigurationsController.RefreshCache) },
        { typeof(InstitutionsController), nameof(InstitutionsController.Create) },
        { typeof(InstitutionsController), nameof(InstitutionsController.SetActive) },
        { typeof(SystemLogsController), nameof(SystemLogsController.CreateRetentionPolicy) },
        { typeof(SystemLogsController), nameof(SystemLogsController.DeleteRetentionPolicy) }
    };

    [Theory]
    [MemberData(nameof(CriticalActions))]
    public void CriticalSystemAdminAction_ShouldRequireMfaPolicy(Type controller, string actionName)
    {
        var action = controller.GetMethod(actionName);

        action.Should().NotBeNull();
        action!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should().Contain(attribute => attribute.Policy == "MfaRequired");
    }

    [Fact]
    public async Task MfaPolicy_ShouldRequireAuthenticatedMfaClaim()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCustomAuthorization();
        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await policyProvider.GetPolicyAsync("MfaRequired");

        policy.Should().NotBeNull();
        policy!.Requirements.OfType<ClaimsAuthorizationRequirement>()
            .Should().ContainSingle(claims =>
                claims.ClaimType == "amr"
                && claims.AllowedValues!.Contains("mfa"));
    }
}
