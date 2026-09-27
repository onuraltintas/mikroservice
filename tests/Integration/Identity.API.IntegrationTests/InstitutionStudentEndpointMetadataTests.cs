using FluentAssertions;
using Identity.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class InstitutionStudentEndpointMetadataTests
{
    [Fact]
    public void Coaching_institution_membership_endpoints_are_authenticated_and_use_the_institution_route()
    {
        var controller = typeof(InstitutionController);

        controller.GetMethod(nameof(InstitutionController.GetStudents)).Should().NotBeNull();
        controller.GetMethod(nameof(InstitutionController.CreateStudent)).Should().NotBeNull();
        controller.GetMethod(nameof(InstitutionController.UpdateStudent)).Should().NotBeNull();
        controller.GetMethod(nameof(InstitutionController.RemoveStudent)).Should().NotBeNull();
        controller.GetMethod(nameof(InstitutionController.InviteTeacher)).Should().NotBeNull();
        controller.GetMethod(nameof(InstitutionController.InviteStudent)).Should().NotBeNull();
        controller.GetMethod(nameof(InstitutionController.RemoveTeacher)).Should().NotBeNull();

        controller.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should().ContainSingle(attribute => attribute.Roles == "InstitutionAdmin,InstitutionOwner");
        controller.GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Should().ContainSingle(attribute => attribute.Template == "api/institution");
    }

    [Fact]
    public void Student_membership_update_contract_does_not_accept_identity_name_changes()
    {
        typeof(InstitutionController.UpdateInstitutionStudentRequest)
            .GetProperties()
            .Select(property => property.Name)
            .Should()
            .BeEquivalentTo("GradeLevel", "TeacherUserId");
    }
}
