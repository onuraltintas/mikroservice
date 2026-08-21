using FluentAssertions;
using Identity.API.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Identity.API.IntegrationTests;

public sealed class ParentChildrenMetadataTests
{
    [Fact]
    public void Children_endpoint_requires_parent_role_and_is_not_anonymous()
    {
        var action = typeof(UserController).GetMethod(nameof(UserController.GetMyChildren));

        action.Should().NotBeNull();
        var authorize = action!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        authorize.Roles.Should().Be("Parent");
        action.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Should().BeEmpty();
    }
}
