using EduPlatform.Shared.Kernel.Results;
using Identity.API.Controllers;
using Identity.Application.Commands.GoogleLogin;
using Identity.Application.Commands.Login;
using Identity.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Identity.API.IntegrationTests;

public sealed class ProductSessionEndpointTests
{
    [Fact]
    public async Task ProductLogin_ShouldTakeProductFromRoute_NotRequestBody()
    {
        var mediator = DispatchProxy.Create<IMediator, SessionCapturingMediatorProxy>();
        var controller = CreateController(mediator);

        var result = await controller.LoginForProduct(
            "speed-reading",
            new LoginCommand("reader@example.test", "password", Product: PlatformProduct.Coaching),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(PlatformProduct.SpeedReading, ((LoginCommand)((SessionCapturingMediatorProxy)(object)mediator).Request!).Product);
    }

    [Fact]
    public async Task GenericLogin_ShouldIgnoreAnyBodySuppliedProduct()
    {
        var mediator = DispatchProxy.Create<IMediator, SessionCapturingMediatorProxy>();
        var controller = CreateController(mediator);

        await controller.Login(
            new LoginCommand("reader@example.test", "password", Product: PlatformProduct.SpeedReading),
            CancellationToken.None);

        Assert.Null(((LoginCommand)((SessionCapturingMediatorProxy)(object)mediator).Request!).Product);
    }

    [Fact]
    public async Task ProductGoogleLogin_ShouldTakeProductFromRoute()
    {
        var mediator = DispatchProxy.Create<IMediator, SessionCapturingMediatorProxy>();
        var controller = CreateController(mediator);

        var result = await controller.GoogleLoginForProduct(
            "coaching",
            new GoogleLoginRequest("google-id-token"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(PlatformProduct.Coaching, ((GoogleLoginCommand)((SessionCapturingMediatorProxy)(object)mediator).Request!).Product);
    }

    private static AuthController CreateController(IMediator mediator)
    {
        var controller = new AuthController(mediator, new TestWebHostEnvironment());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private class SessionCapturingMediatorProxy : DispatchProxy
    {
        public object? Request { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Request = args?[0];
            return Task.FromResult(Result.Success(LoginResponse.RequireMfa("challenge", enrollmentRequired: false)));
        }
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
}
