using FluentAssertions;
using Identity.API.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Identity.API.IntegrationTests;

public sealed class TeacherStudentsMetadataTests
{
    [Fact]
    public void My_students_endpoint_requires_teacher_role_and_is_not_anonymous()
    {
        var action = typeof(TeachersController).GetMethod(nameof(TeachersController.GetMyStudents));

        action.Should().NotBeNull();
        var authorize = action!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single(attribute => !string.IsNullOrWhiteSpace(attribute.Roles));

        authorize.Roles.Should().Be("Teacher");
        action.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Should().BeEmpty();
    }
}
