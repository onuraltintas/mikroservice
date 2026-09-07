using FluentAssertions;
using Identity.Application.Commands.CreateUser;

namespace Identity.API.IntegrationTests;

public sealed class CreateUserRoleTests
{
    [Fact]
    public async Task Validator_accepts_a_role_created_by_the_role_management_api()
    {
        var result = await new CreateUserCommandValidator().ValidateAsync(
            new CreateUserCommand(
                "Custom",
                "Role User",
                "custom-role@example.com",
                null,
                "ContentReviewer"));

        result.IsValid.Should().BeTrue();
    }
}
