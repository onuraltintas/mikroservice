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

    // Paket F #10: the page shows one message per reason, so the answer names it.
    [Theory]
    [InlineData(ConfirmEmailOutcome.Confirmed, "confirmed")]
    [InlineData(ConfirmEmailOutcome.AlreadyConfirmed, "alreadyConfirmed")]
    public async Task ConfirmEmail_Success_NamesWhetherTheAddressWasAlreadyVerified(ConfirmEmailOutcome outcome, string status)
    {
        var result = await CreateConfirmController(Result.Success(outcome))
            .ConfirmEmail(new ConfirmEmailCommand(Guid.NewGuid(), "token"));

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(status, Assert.IsType<ConfirmEmailResponse>(ok.Value).Status);
    }

    [Fact]
    public async Task ConfirmEmail_ExpiredLink_KeepsItsReasonCode()
    {
        var result = await CreateConfirmController(Result.Failure<ConfirmEmailOutcome>(ConfirmEmailErrors.TokenExpired))
            .ConfirmEmail(new ConfirmEmailCommand(Guid.NewGuid(), "token"));

        Assert.Equal("User.TokenExpired", Assert.IsType<Error>(Assert.IsType<BadRequestObjectResult>(result).Value).Code);
    }

    // An unknown account id answers exactly like a wrong link, and a missing token too.
    [Fact]
    public async Task ConfirmEmail_UnknownAccountMissingAndWrongToken_AnswerTheSameInvalidLink()
    {
        var unknown = await CreateConfirmController(Result.Failure<ConfirmEmailOutcome>(new Error("User.NotFound", "Kullanıcı bulunamadı.")))
            .ConfirmEmail(new ConfirmEmailCommand(Guid.NewGuid(), "token"));
        var wrong = await CreateConfirmController(Result.Failure<ConfirmEmailOutcome>(ConfirmEmailErrors.InvalidToken))
            .ConfirmEmail(new ConfirmEmailCommand(Guid.NewGuid(), "token"));
        var missing = await new AuthController(DispatchProxy.Create<IMediator, ThrowingMediatorProxy>(), new TestWebHostEnvironment())
            .ConfirmEmail(new ConfirmEmailCommand(Guid.NewGuid()));

        var errors = new[] { unknown, wrong, missing }
            .Select(r => Assert.IsType<Error>(Assert.IsType<BadRequestObjectResult>(r).Value)).ToArray();
        Assert.All(errors, e => Assert.Equal("User.InvalidToken", e.Code));
        Assert.Single(errors.Select(e => e.Description).Distinct());
    }

    private static AuthController CreateConfirmController(Result<ConfirmEmailOutcome> answer)
    {
        var mediator = DispatchProxy.Create<IMediator, ConfirmMediatorProxy>();
        ((ConfirmMediatorProxy)(object)mediator).Answer = answer;
        return new AuthController(mediator, new TestWebHostEnvironment());
    }

    private class ConfirmMediatorProxy : DispatchProxy
    {
        public Result<ConfirmEmailOutcome> Answer { get; set; } = Result.Success(ConfirmEmailOutcome.Confirmed);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Send" && args?.FirstOrDefault() is ConfirmEmailCommand)
                return Task.FromResult(Answer);
            throw new InvalidOperationException("Unexpected mediator call.");
        }
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
