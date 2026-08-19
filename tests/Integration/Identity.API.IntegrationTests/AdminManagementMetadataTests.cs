using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Identity.API.Controllers;
using Identity.API.Controllers.Settings;
using Microsoft.AspNetCore.Authorization;
using Notification.API.Controllers;

namespace Identity.API.IntegrationTests;

public class AdminManagementMetadataTests
{
    [Fact]
    public void InstitutionsController_MustRequirePermissionMetadata()
    {
        typeof(InstitutionsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Should()
            .Contain(attribute => attribute is HasPermissionAttribute);
    }

    [Fact]
    public void InstitutionMutations_MustRequireManagePermission()
    {
        foreach (var methodName in new[] { "Create", "Update", "SetActive", "AssignAdmin", "SetAdminActive" })
        {
            var method = typeof(InstitutionsController).GetMethod(methodName);
            method.Should().NotBeNull();
            method!.GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
                .Cast<HasPermissionAttribute>()
                .Should()
                .Contain(attribute => attribute.Policy == "Permissions.Institutions.Manage");
        }

        foreach (var methodName in new[] { "Create", "SetActive" })
        {
            typeof(InstitutionsController)
                .GetMethod(methodName)!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .Should()
                .Contain(attribute => attribute.Roles == "SystemAdmin");
        }
    }

    [Fact]
    public void UserMutations_MustRequireSystemAdministrator()
    {
        foreach (var methodName in new[]
                 {
                     "CreateUser", "DeleteUser", "ActivateUser", "ConfirmEmail",
                     "ChangePassword", "UpdateUser", "AssignRole", "RemoveRole"
                 })
        {
            typeof(UserController)
                .GetMethod(methodName)!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .Should()
                .Contain(attribute => attribute.Roles == "SystemAdmin");
        }
    }

    [Fact]
    public void SupportAdminController_MustRequirePermissionMetadata()
    {
        typeof(SupportAdminController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Should()
            .Contain(attribute => attribute is HasPermissionAttribute);
    }

    [Fact]
    public void EmailTemplatesController_MustRequireTemplatePermission()
    {
        typeof(EmailTemplatesController)
            .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
            .Cast<HasPermissionAttribute>()
            .Should()
            .Contain(attribute => attribute.Policy == "Permissions.Notifications.Templates");
    }

    [Fact]
    public void OperationalReadControllers_MustRequireOperationsPermission()
    {
        typeof(ConfigurationsController)
            .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
            .Cast<HasPermissionAttribute>()
            .Should()
            .Contain(attribute => attribute.Policy == "Permissions.Operations.View");

        typeof(SystemLogsController)
            .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
            .Cast<HasPermissionAttribute>()
            .Should()
            .Contain(attribute => attribute.Policy == "Permissions.Operations.View");
    }
}
