using EduPlatform.Shared.Infrastructure.Behaviors;
using EduPlatform.Shared.Kernel.Results;
using FluentAssertions;
using Identity.Application.Commands.ChangePassword;
using Identity.Application.Commands.Login;
using MediatR;

namespace Identity.API.IntegrationTests;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task LoginValidation_ShouldRejectOversizedPasswordsBeforeHandler()
    {
        var behavior = new ValidationBehavior<LoginCommand, Result<LoginResponse>>(
            new[] { new LoginCommandValidator() });
        var command = new LoginCommand("user@example.com", new string('a', 129));
        var handlerCalled = false;
        RequestHandlerDelegate<Result<LoginResponse>> next = _ =>
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success(new LoginResponse("access", "refresh")));
        };

        var action = () => behavior.Handle(command, next, CancellationToken.None);

        await action.Should().ThrowAsync<EduPlatform.Shared.Kernel.Exceptions.ValidationException>();
        handlerCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ChangePasswordValidation_ShouldRejectOversizedPasswordsBeforeHandler()
    {
        var behavior = new ValidationBehavior<ChangePasswordCommand, Result>(
            new[] { new ChangePasswordCommandValidator() });
        var command = new ChangePasswordCommand("Current123!", new string('a', 129));
        var handlerCalled = false;
        RequestHandlerDelegate<Result> next = _ =>
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        };

        var action = () => behavior.Handle(command, next, CancellationToken.None);

        await action.Should().ThrowAsync<EduPlatform.Shared.Kernel.Exceptions.ValidationException>();
        handlerCalled.Should().BeFalse();
    }
}
