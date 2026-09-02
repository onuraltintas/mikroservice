using Coaching.API.Controllers;
using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAdminManagementMetadataTests
{
    [Fact]
    public void CoachingAdminOverview_MustRequireAuthenticatedPermissionWithoutGlobalRoleAssumption()
    {
        var authorizeAttributes = typeof(CoachingAdminController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>();

        authorizeAttributes.Should().Contain(attribute =>
            string.IsNullOrWhiteSpace(attribute.Roles)
            && string.IsNullOrWhiteSpace(attribute.Policy));

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
    [InlineData(nameof(CoachingAdminController.UpdateAssignment))]
    [InlineData(nameof(CoachingAdminController.CreateSession))]
    [InlineData(nameof(CoachingAdminController.UpdateSessionAttendance))]
    [InlineData(nameof(CoachingAdminController.CancelSession))]
    [InlineData(nameof(CoachingAdminController.DeleteSession))]
    [InlineData(nameof(CoachingAdminController.UpdateSession))]
    [InlineData(nameof(CoachingAdminController.CreateExam))]
    [InlineData(nameof(CoachingAdminController.AddExamResult))]
    [InlineData(nameof(CoachingAdminController.UpdateExam))]
    [InlineData(nameof(CoachingAdminController.UpdateExamResult))]
    [InlineData(nameof(CoachingAdminController.DeleteExamResult))]
    [InlineData(nameof(CoachingAdminController.DeleteExam))]
    [InlineData(nameof(CoachingAdminController.CreateGoal))]
    [InlineData(nameof(CoachingAdminController.UpdateGoalProgress))]
    [InlineData(nameof(CoachingAdminController.UpdateGoal))]
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
            .Contain(attribute => attribute.Policy == "MfaRequired")
            .And.Contain(attribute => attribute.Roles == "SystemAdmin");
    }

    [Fact]
    public void DeleteExamResult_MustUseDelete()
    {
        typeof(CoachingAdminController)
            .GetMethod(nameof(CoachingAdminController.DeleteExamResult))!
            .GetCustomAttributes(typeof(HttpDeleteAttribute), inherit: true)
            .Should()
            .NotBeEmpty();
    }

    [Theory]
    [InlineData(nameof(CoachingAdminController.UpdateAssignment))]
    [InlineData(nameof(CoachingAdminController.UpdateSession))]
    [InlineData(nameof(CoachingAdminController.UpdateExam))]
    [InlineData(nameof(CoachingAdminController.UpdateExamResult))]
    [InlineData(nameof(CoachingAdminController.UpdateGoal))]
    public void CoachingEditActions_MustUsePut(string actionName)
    {
        typeof(CoachingAdminController)
            .GetMethod(actionName)!
            .GetCustomAttributes(typeof(HttpPutAttribute), inherit: true)
            .Should()
            .NotBeEmpty();
    }
}
