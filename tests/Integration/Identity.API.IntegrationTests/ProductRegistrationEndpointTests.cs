using Identity.API.Controllers;
using Identity.Application.Commands.RegisterStudent;
using Identity.Application.Commands.RegisterTeacher;
using Identity.Application.Commands.RegisterInstitution;
using Identity.Application.Commands.RegisterParent;
using Identity.Application.Commands.Login;
using Identity.Application.Commands.GoogleLogin;
using Identity.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Identity.API.IntegrationTests;

public sealed class ProductRegistrationEndpointTests
{
    [Fact]
    public async Task RegisterStudent_ShouldUseProductFromRouteInsteadOfRequestBody()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = new AuthController(mediator, new TestWebHostEnvironment());

        var result = await controller.RegisterStudent(
            "speed-reading",
            new RegisterStudentCommand(
                "student@example.test",
                "StrongPassword1!",
                "Test",
                "Student",
                null,
                PlatformProduct.Coaching));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(
            PlatformProduct.SpeedReading,
            Assert.IsType<RegisterStudentCommand>(capture.Request).Product);
    }

    [Fact]
    public void RegisterStudent_ShouldBeMountedOnlyUnderProductScopedRoute()
    {
        var templates = typeof(AuthController)
            .GetMethod(nameof(AuthController.RegisterStudent))!
            .GetCustomAttributes<HttpPostAttribute>()
            .Select(attribute => attribute.Template)
            .ToArray();

        Assert.Contains("{product:regex(coaching|speed-reading)}/register/student", templates);
        Assert.DoesNotContain("register-student", templates);
    }

    [Fact]
    public async Task LoginForProduct_ShouldUseProductFromRouteInsteadOfRequestBody()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = new AuthController(mediator, new TestWebHostEnvironment())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.LoginForProduct(
            "speed-reading",
            new LoginCommand("student@example.test", "password", Product: PlatformProduct.Coaching));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(
            PlatformProduct.SpeedReading,
            Assert.IsType<LoginCommand>(capture.Request).Product);
    }

    [Fact]
    public async Task GenericLogin_ShouldIgnoreProductSuppliedInRequestBody()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = new AuthController(mediator, new TestWebHostEnvironment())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Login(
            new LoginCommand("admin@example.test", "password", Product: PlatformProduct.SpeedReading));

        Assert.IsType<OkObjectResult>(result);
        Assert.Null(Assert.IsType<LoginCommand>(capture.Request).Product);
    }

    [Fact]
    public async Task GoogleLoginForProduct_ShouldUseProductFromRoute()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = new AuthController(mediator, new TestWebHostEnvironment())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.GoogleLoginForProduct(
            "speed-reading",
            new GoogleLoginRequest("google-id-token"));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(
            PlatformProduct.SpeedReading,
            Assert.IsType<GoogleLoginCommand>(capture.Request).Product);
    }

    [Fact]
    public void GoogleLoginForProduct_ShouldBeMountedOnlyUnderProductScopedRoute()
    {
        var templates = typeof(AuthController)
            .GetMethod(nameof(AuthController.GoogleLoginForProduct))!
            .GetCustomAttributes<HttpPostAttribute>()
            .Select(attribute => attribute.Template)
            .ToArray();

        Assert.Contains("{product:regex(coaching|speed-reading)}/google-login", templates);
        Assert.DoesNotContain("google-login", templates);
    }

    [Fact]
    public void UserRoleAssignment_ShouldBeMountedOnlyUnderProductScopedRoute()
    {
        var templates = typeof(UserController)
            .GetMethod(nameof(UserController.AssignRole))!
            .GetCustomAttributes<HttpPostAttribute>()
            .Select(attribute => attribute.Template)
            .ToArray();

        Assert.Contains("{id:guid}/products/{product:regex(coaching|speed-reading)}/roles", templates);
        Assert.DoesNotContain("{id:guid}/roles", templates);
    }

    [Fact]
    public void UserRoleRemoval_ShouldExposeProductScopedRoute()
    {
        var templates = typeof(UserController)
            .GetMethod(nameof(UserController.RemoveProductRole))!
            .GetCustomAttributes<HttpDeleteAttribute>()
            .Select(attribute => attribute.Template)
            .ToArray();

        Assert.Contains("{id:guid}/products/{product:regex(coaching|speed-reading)}/roles/{roleName}", templates);
    }

    [Fact]
    public async Task RegistrationHandlers_ShouldRejectMissingProductScope()
    {
        var studentHandler = new RegisterStudentCommandHandler(null!, null!, null!, null!, null!, null!, null!);
        var teacherHandler = new RegisterTeacherCommandHandler(null!, null!, null!, null!, null!, null!, null!);
        var institutionHandler = new RegisterInstitutionCommandHandler(null!, null!, null!, null!, null!, null!, null!, null!);

        var studentResult = await studentHandler.Handle(
            new RegisterStudentCommand("student@example.test", "password", "Test", "Student", null),
            CancellationToken.None);
        var teacherResult = await teacherHandler.Handle(
            new RegisterTeacherCommand("teacher@example.test", "password", "Test", "Teacher", null),
            CancellationToken.None);
        var institutionResult = await institutionHandler.Handle(
            new RegisterInstitutionCommand(
                "institution@example.test", "password", "Test", "Owner", "School",
                Identity.Domain.Enums.InstitutionType.School, null, "province", "district"),
            CancellationToken.None);

        Assert.Equal("Auth.ProductRequired", studentResult.Error.Code);
        Assert.Equal("Auth.ProductRequired", teacherResult.Error.Code);
        Assert.Equal("Auth.ProductRequired", institutionResult.Error.Code);
    }

    [Fact]
    public async Task ParentRegistration_ShouldRejectNonCoachingProduct()
    {
        var handler = new RegisterParentCommandHandler(null!, null!, null!, null!, null!, null!, null!);

        var result = await handler.Handle(
            new RegisterParentCommand(
                "parent@example.test", "password", "Test", "Parent", null, PlatformProduct.SpeedReading),
            CancellationToken.None);

        Assert.Equal("Auth.ProductRoleNotSupported", result.Error.Code);
    }

    private class CapturingMediatorProxy : DispatchProxy
    {
        public object? Request { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Send")
            {
                Request = args?.FirstOrDefault(argument => argument is not CancellationToken);
                if (Request is LoginCommand)
                {
                    return Task.FromResult(EduPlatform.Shared.Kernel.Results.Result.Success(
                        new LoginResponse("access-token", "refresh-token", DateTime.UtcNow.AddDays(1), true)));
                }
                if (Request is GoogleLoginCommand)
                {
                    return Task.FromResult(EduPlatform.Shared.Kernel.Results.Result.Success(
                        new LoginResponse("access-token", "refresh-token", DateTime.UtcNow.AddDays(1), true)));
                }
                return Task.FromResult(EduPlatform.Shared.Kernel.Results.Result.Success(Guid.NewGuid()));
            }

            throw new NotSupportedException($"Unexpected mediator call: {targetMethod?.Name}");
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
