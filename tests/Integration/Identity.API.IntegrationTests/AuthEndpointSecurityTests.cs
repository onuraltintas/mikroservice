using Identity.API.Controllers;
using Identity.Application.Commands.ConfirmEmail;
using Identity.Application.Commands.ResetPassword;
using Identity.Application.Commands.ResendVerificationEmail;
using EduPlatform.Shared.Kernel.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using Xunit;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;

namespace Identity.API.IntegrationTests;

public class AuthEndpointSecurityTests
{
    [Fact]
    public async Task ConfirmEmail_WithoutToken_ShouldBeRejectedBeforeHandlerExecution()
    {
        var mediator = DispatchProxy.Create<IMediator, ThrowingMediatorProxy>();
        var controller = new AuthController(mediator, new TestWebHostEnvironment());

        var result = await controller.ConfirmEmail(new ConfirmEmailCommand(Guid.NewGuid()));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ResetPassword_UnknownAccountAndInvalidToken_ShouldReturnSameGenericResponse()
    {
        var unknownAccount = await CreateController(new Error(
                "ResetPassword.UserNotFound",
                "Kullanıcı bulunamadı."))
            .ResetPassword(new ResetPasswordCommand("unknown@example.com", "token", "NewPassword123!"));
        var invalidToken = await CreateController(new Error(
                "ResetPassword.InvalidToken",
                "Geçersiz sıfırlama kodu."))
            .ResetPassword(new ResetPasswordCommand("known@example.com", "token", "NewPassword123!"));
        var expiredToken = await CreateController(new Error(
                "ResetPassword.ExpiredToken",
                "Sıfırlama kodunun süresi dolmuş."))
            .ResetPassword(new ResetPasswordCommand("known@example.com", "expired-token", "NewPassword123!"));

        var unknownResponse = Assert.IsType<BadRequestObjectResult>(unknownAccount);
        var invalidResponse = Assert.IsType<BadRequestObjectResult>(invalidToken);
        var expiredResponse = Assert.IsType<BadRequestObjectResult>(expiredToken);
        var unknownError = Assert.IsType<Error>(unknownResponse.Value);
        var invalidError = Assert.IsType<Error>(invalidResponse.Value);
        var expiredError = Assert.IsType<Error>(expiredResponse.Value);

        Assert.Equal(unknownResponse.StatusCode, invalidResponse.StatusCode);
        Assert.Equal(unknownResponse.StatusCode, expiredResponse.StatusCode);
        Assert.Equal(unknownError.Code, invalidError.Code);
        Assert.Equal(unknownError.Description, invalidError.Description);
        Assert.Equal(unknownError.Code, expiredError.Code);
        Assert.Equal(unknownError.Description, expiredError.Description);
    }

    [Theory]
    [InlineData("User.NotFound", "Kullanıcı bulunamadı.")]
    [InlineData("User.EmailAlreadyConfirmed", "E-posta adresi zaten doğrulanmış.")]
    public async Task ResendVerification_KnownAndUnknownAccountStates_ShouldReturnSuccess(
        string errorCode,
        string description)
    {
        var result = await CreateController(new Error(errorCode, description))
            .ResendVerificationEmail(new ResendVerificationEmailCommand("user@example.com"));

        Assert.IsType<OkResult>(result);
    }

    private static AuthController CreateController(Error failure)
    {
        var mediator = DispatchProxy.Create<IMediator, FailureMediatorProxy>();
        ((FailureMediatorProxy)(object)mediator).Failure = failure;
        return new AuthController(mediator, new TestWebHostEnvironment());
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private class ThrowingMediatorProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            throw new InvalidOperationException("The mediator should not be called for a missing confirmation token.");
        }
    }

    private class FailureMediatorProxy : DispatchProxy
    {
        public Error Failure { get; set; } = new("Test.Failure", "Test failure.");

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Send"
                && args?.FirstOrDefault() is IRequest<Result>)
            {
                return Task.FromResult(Result.Failure(Failure));
            }

            throw new InvalidOperationException("Unexpected mediator call.");
        }
    }
}
