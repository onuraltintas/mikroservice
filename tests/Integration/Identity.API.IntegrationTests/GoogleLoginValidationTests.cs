using FluentAssertions;
using Identity.Application.Commands.GoogleLogin;

namespace Identity.API.IntegrationTests;

public sealed class GoogleLoginValidationTests
{
    private readonly GoogleLoginCommandValidator _validator = new();

    [Fact]
    public async Task EmptyIdToken_ShouldBeRejected()
    {
        var result = await _validator.ValidateAsync(new GoogleLoginCommand("", "127.0.0.1"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GoogleLoginCommand.IdToken));
    }

    [Fact]
    public async Task OversizedIdToken_ShouldBeRejected()
    {
        var oversizedToken = new string('a', 16_385);

        var result = await _validator.ValidateAsync(new GoogleLoginCommand(oversizedToken, "127.0.0.1"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GoogleLoginCommand.IdToken));
    }
}
