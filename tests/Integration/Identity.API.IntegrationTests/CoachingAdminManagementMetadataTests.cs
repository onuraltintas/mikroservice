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

    [Theory]
    [InlineData(nameof(CoachingAdminController.CreateAssignment))]
    [InlineData(nameof(CoachingAdminController.CancelAssignment))]
    [InlineData(nameof(CoachingAdminController.DeleteAssignment))]
    [InlineData(nameof(CoachingAdminController.GradeAssignment))]
    [InlineData(nameof(CoachingAdminController.CreateSession))]
    [InlineData(nameof(CoachingAdminController.UpdateSessionAttendance))]
    [InlineData(nameof(CoachingAdminController.CancelSession))]
    [InlineData(nameof(CoachingAdminController.DeleteSession))]
    [InlineData(nameof(CoachingAdminController.CreateExam))]
    [InlineData(nameof(CoachingAdminController.AddExamResult))]
    [InlineData(nameof(CoachingAdminController.DeleteExam))]
    [InlineData(nameof(CoachingAdminController.CreateGoal))]
    [InlineData(nameof(CoachingAdminController.UpdateGoalProgress))]
    [InlineData(nameof(CoachingAdminController.DeleteGoal))]
    public void CoachingManagementActions_MustRequireCoachingManagePermission(string actionName)
    {
        var method = typeof(CoachingAdminController).GetMethod(actionName);

        method.Should().NotBeNull();
        method!
            .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
            .Cast<HasPermissionAttribute>()
            .Should()
            .Contain(attribute => attribute.Policy == "Permissions.Coaching.Manage");

        method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .Contain(attribute => attribute.Policy == "MfaRequired");
    }
}
