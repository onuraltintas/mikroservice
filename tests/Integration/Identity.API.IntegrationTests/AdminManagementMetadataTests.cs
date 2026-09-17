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
                     "ChangePassword", "UpdateUser", "UpdateUserProfile", "AssignRole", "RemoveRole"
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
    public void BulkUserMutations_MustRequireSystemAdministratorAndStepUp()
    {
        foreach (var methodName in new[] { "Import", "AssignRole" })
        {
            var method = typeof(BulkUsersController).GetMethod(methodName);
            method.Should().NotBeNull();
            method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .Should()
                .Contain(attribute => attribute.Roles == "SystemAdmin")
                .And.Contain(attribute => attribute.Policy == "MfaRequired");
        }

        typeof(BulkUsersController)
            .GetMethod(nameof(BulkUsersController.Export))!
            .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
            .Cast<HasPermissionAttribute>()
            .Should()
            .Contain(attribute => attribute.Policy == "Permissions.Users.View");
    }

    [Fact]
    public void UserProfileMutation_MustRequireEditPermission()
    {
        var method = typeof(UserController).GetMethod(nameof(UserController.UpdateUserProfile));

        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
            .Cast<HasPermissionAttribute>()
            .Should()
            .Contain(attribute => attribute.Policy == "Permissions.Users.Edit");
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
    public void NotificationReadEndpoints_ShouldNotRequireMfaStepUp()
    {
        foreach (var (controller, methodName) in new[]
                 {
                     (typeof(SupportAdminController), nameof(SupportAdminController.GetAll)),
                     (typeof(SupportAdminController), nameof(SupportAdminController.GetById)),
                     (typeof(EmailTemplatesController), nameof(EmailTemplatesController.GetAll))
                 })
        {
            var method = controller.GetMethod(methodName);
            method.Should().NotBeNull();
            method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .Should().NotContain(attribute => attribute.Policy == "MfaRequired");
            method.GetCustomAttributes(typeof(MfaCategoryAttribute), inherit: true)
                .Should().BeEmpty();
        }
    }

    [Fact]
    public void NotificationMutationEndpoints_ShouldRequireCmsMfaStepUp()
    {
        foreach (var (controller, methodName) in new[]
                 {
                     (typeof(SupportAdminController), nameof(SupportAdminController.Process)),
                     (typeof(SupportAdminController), nameof(SupportAdminController.Reply)),
                     (typeof(EmailTemplatesController), nameof(EmailTemplatesController.Create)),
                     (typeof(EmailTemplatesController), nameof(EmailTemplatesController.Update))
                 })
        {
            var method = controller.GetMethod(methodName);
            method.Should().NotBeNull();
            method!.GetCustomAttributes(typeof(MfaCategoryAttribute), inherit: true)
                .Cast<MfaCategoryAttribute>()
                .Should().ContainSingle(attribute => attribute.Category == MfaOperationCategories.Cms);
        }
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
