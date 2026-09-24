using Coaching.API.Controllers;
using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class InstitutionCoachingMetadataTests
{
    [Fact]
    public void InstitutionWrites_MustRequireInstitutionRoleCoachingViewAndConfiguredMfa()
    {
        var type = typeof(InstitutionCoachingController);
        var authorization = type.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().ToArray();

        authorization.Should().Contain(attribute => attribute.Roles == "InstitutionAdmin,InstitutionOwner");
        authorization.Should().Contain(attribute => attribute.Policy == "MfaRequired");
        type.GetCustomAttributes(typeof(HasPermissionAttribute), true)
            .Cast<HasPermissionAttribute>()
            .Should().Contain(attribute => attribute.Policy == "Permissions.Coaching.View");
        type.GetCustomAttributes(typeof(MfaCategoryAttribute), true)
            .Should().NotBeEmpty();
        type.GetCustomAttributes(typeof(RouteAttribute), true)
            .Cast<RouteAttribute>()
            .Should().ContainSingle(attribute => attribute.Template == "api/coaching-admin/institution");
    }

    [Theory]
    [InlineData(nameof(InstitutionCoachingController.CreateAssignment))]
    [InlineData(nameof(InstitutionCoachingController.CreateSession))]
    [InlineData(nameof(InstitutionCoachingController.UpdateAssignment))]
    [InlineData(nameof(InstitutionCoachingController.CancelAssignment))]
    [InlineData(nameof(InstitutionCoachingController.GradeAssignment))]
    [InlineData(nameof(InstitutionCoachingController.UpdateSession))]
    [InlineData(nameof(InstitutionCoachingController.CancelSession))]
    [InlineData(nameof(InstitutionCoachingController.UpdateAttendance))]
    public void InstitutionWriteActions_MustBeExplicitlyMapped(string actionName)
    {
        typeof(InstitutionCoachingController).GetMethod(actionName).Should().NotBeNull();
    }
}
