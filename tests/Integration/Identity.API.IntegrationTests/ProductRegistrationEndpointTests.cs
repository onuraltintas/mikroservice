using EduPlatform.Shared.Kernel.Results;
using Identity.API.Controllers;
using Identity.Application.Commands.RegisterStudent;
using Identity.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Identity.API.IntegrationTests;

public sealed class ProductRegistrationEndpointTests
{
    [Theory]
    [InlineData("coaching", PlatformProduct.Coaching)]
    [InlineData("speed-reading", PlatformProduct.SpeedReading)]
    public async Task RegisterStudent_ShouldUseProductFromRoute_NotRequestBody(
        string productRoute,
        PlatformProduct expectedProduct)
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var controller = new AuthController(mediator, new TestWebHostEnvironment());

        await controller.RegisterStudent(
            productRoute,
            new RegisterStudentCommand(
                "student@example.test",
                "StrongPassword123!",
                "Test",
                "Student",
                null,
                PlatformProduct.Coaching),
            CancellationToken.None);

        var proxy = (CapturingMediatorProxy)(object)mediator;
        Assert.IsType<RegisterStudentCommand>(proxy.Request);
        Assert.Equal(expectedProduct, ((RegisterStudentCommand)proxy.Request!).Product);
    }

    [Fact]
    public void RegisterStudent_ShouldNotExposeAnUnscopedLegacyRoute()
    {
        var routes = typeof(AuthController)
            .GetMethod(nameof(AuthController.RegisterStudent))!
            .GetCustomAttributes<HttpPostAttribute>()
            .Select(attribute => attribute.Template)
            .ToArray();

        Assert.Equal(new[] { "{product:regex(coaching|speed-reading)}/register/student" }, routes);
    }

    private class CapturingMediatorProxy : DispatchProxy
    {
        public object? Request { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Request = args?[0];
            return Task.FromResult(Result.Success(Guid.NewGuid()));
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
