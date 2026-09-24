using FluentAssertions;
using Identity.Application.Commands.CreateUser;

namespace Identity.API.IntegrationTests;

public sealed class CreateUserRoleTests
{
    [Theory]
    [InlineData("InstitutionAdmin")]
    [InlineData("InstitutionOwner")]
    public async Task Generic_user_creation_rejects_institution_roles_without_membership(string roleName)
    {
        var result = await new CreateUserCommandValidator().ValidateAsync(
            new CreateUserCommand("Institution", "User", "institution@example.com", null, roleName));

        result.IsValid.Should().BeFalse();
    }

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
