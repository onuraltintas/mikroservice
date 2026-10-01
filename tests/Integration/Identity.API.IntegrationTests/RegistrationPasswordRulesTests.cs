using FluentAssertions;
using Identity.Application.Validators;

namespace Identity.API.IntegrationTests;

public class RegistrationPasswordRulesTests
{
    [Theory]
    [InlineData("Password123!", true)]
    [InlineData("Password123 ", true)]
    [InlineData("password123!", false)]
    [InlineData("Password123", false)]
    [InlineData("password", false)]
    [InlineData("", false)]
    public void IsValid_ShouldApplyTheSameCharacterAndLengthPolicy(string password, bool expected)
    {
        RegistrationPasswordRules.IsValid(password).Should().Be(expected);
    }

    [Fact]
    public void IsValid_ShouldAcceptTheMaximumLength()
    {
        var password = "Password123!" + new string('a', 116);

        RegistrationPasswordRules.IsValid(password).Should().BeTrue();
    }

    [Fact]
    public void IsValid_ShouldRejectPasswordsLongerThanTheMaximum()
    {
        var password = "Password123!" + new string('a', 117);

        RegistrationPasswordRules.IsValid(password).Should().BeFalse();
    }
}
