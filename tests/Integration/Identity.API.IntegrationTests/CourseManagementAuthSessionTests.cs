using EduPlatform.Shared.Kernel.Results;
using Identity.API.Controllers;
using Identity.API.Security;
using Identity.Application.Commands.Login;
using Identity.Application.Commands.RefreshToken;
using Identity.Application.Commands.RevokeToken;
using Identity.Application.Commands.OpenCourseManagementSession;
using Identity.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using System.Security.Claims;

namespace Identity.API.IntegrationTests;

public sealed class CourseManagementAuthSessionTests
{
    [Fact]
    public async Task LoginForProduct_ShouldUseSelectedSourceProductAndCourseScopedCookie()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var controller = CreateController(mediator);

        var result = await controller.LoginForProduct(
            "speed-reading",
            new LoginCommand("student@example.test", "password", Product: PlatformProduct.CourseManagement),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(PlatformProduct.SpeedReading, Assert.IsType<LoginCommand>(capture.Request).Product);
        Assert.Contains(RefreshTokenCookiePolicy.CourseManagementCookieName, controller.Response.Headers.SetCookie.ToString());
        Assert.Contains("path=/api/auth/course-management", controller.Response.Headers.SetCookie.ToString());
        Assert.DoesNotContain(RefreshTokenCookiePolicy.CookieName + "=", controller.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task OpenSession_ShouldReadCourseCookieAndIssueCourseProductSessionForCurrentUser()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var userId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{RefreshTokenCookiePolicy.CourseManagementCookieName}=course-refresh-token";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString())], "test"));
        var controller = CreateController(mediator, httpContext);

        var result = await controller.OpenSession(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var command = Assert.IsType<RefreshTokenCommand>(capture.Request);
        Assert.Equal("course-refresh-token", command.RefreshToken);
        Assert.Equal(PlatformProduct.CourseManagement, command.TargetProduct);
        Assert.True(command.AllowCourseManagementStudentSession);
        Assert.Equal(userId, command.ExpectedUserId);
        Assert.Contains(RefreshTokenCookiePolicy.CourseManagementCookieName, controller.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task RefreshToken_ShouldUseCourseCookieAndKeepRefreshOutOfResponseBody()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{RefreshTokenCookiePolicy.CourseManagementCookieName}=course-refresh-token";
        var controller = CreateController(mediator, httpContext);

        var result = await controller.RefreshToken(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var command = Assert.IsType<RefreshTokenCommand>(capture.Request);
        Assert.Equal("course-refresh-token", command.RefreshToken);
        Assert.Null(command.TargetProduct);
        Assert.False(command.AllowCourseManagementStudentSession);
        Assert.Contains(RefreshTokenCookiePolicy.CourseManagementCookieName, controller.Response.Headers.SetCookie.ToString());
        Assert.DoesNotContain("course-refresh-token", System.Text.Json.JsonSerializer.Serialize(((OkObjectResult)result).Value));
    }

    [Fact]
    public async Task OpenSession_WithoutCourseCookie_ShouldIssueIndependentSessionAndLeaveSourceCookieAlone()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var userId = Guid.NewGuid();
        var verifiedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{RefreshTokenCookiePolicy.CookieName}=source-refresh-token";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", userId.ToString()),
            new Claim("amr", "mfa"),
            new Claim("auth_time", verifiedAt.ToUnixTimeSeconds().ToString())
        ], "test"));
        var controller = CreateController(mediator, httpContext);

        var result = await controller.OpenSession(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var command = Assert.IsType<OpenCourseManagementSessionCommand>(capture.Request);
        Assert.Equal(userId, command.UserId);
        Assert.Equal(verifiedAt.ToUnixTimeSeconds(), command.MfaVerifiedAt!.Value.ToUnixTimeSeconds());
        Assert.Contains(RefreshTokenCookiePolicy.CourseManagementCookieName, controller.Response.Headers.SetCookie.ToString());
        Assert.Contains("path=/api/auth/course-management", controller.Response.Headers.SetCookie.ToString());
        Assert.DoesNotContain(RefreshTokenCookiePolicy.CookieName + "=", controller.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task RevokeToken_ShouldRevokeAndClearOnlyCourseCookie()
    {
        var mediator = DispatchProxy.Create<IMediator, CapturingMediatorProxy>();
        var capture = (CapturingMediatorProxy)(object)mediator;
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{RefreshTokenCookiePolicy.CourseManagementCookieName}=course-refresh-token";
        var controller = CreateController(mediator, httpContext);

        var result = await controller.RevokeToken(CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.Equal("course-refresh-token", Assert.IsType<RevokeTokenCommand>(capture.Request).Token);
        Assert.Contains(RefreshTokenCookiePolicy.CourseManagementCookieName, controller.Response.Headers.SetCookie.ToString());
        Assert.Contains("path=/api/auth/course-management", controller.Response.Headers.SetCookie.ToString());
        Assert.DoesNotContain(RefreshTokenCookiePolicy.CookieName + "=", controller.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public void LoginAndMfaRoutes_ShouldRemainInsideCourseManagementAuthScope()
    {
        var loginRoutes = typeof(CourseManagementAuthController)
            .GetMethod(nameof(CourseManagementAuthController.LoginForProduct))!
            .GetCustomAttributes<HttpPostAttribute>()
            .Select(attribute => attribute.Template);
        var mfaRoutes = typeof(CourseManagementAuthController)
            .GetMethod(nameof(CourseManagementAuthController.VerifyMfa))!
            .GetCustomAttributes<HttpPostAttribute>()
            .Select(attribute => attribute.Template);
        var mfaSetupRoutes = typeof(CourseManagementAuthController)
            .GetMethod(nameof(CourseManagementAuthController.StartMfaSetup))!
            .GetCustomAttributes<HttpPostAttribute>()
            .Select(attribute => attribute.Template);
        var mfaEnableRoutes = typeof(CourseManagementAuthController)
            .GetMethod(nameof(CourseManagementAuthController.EnableMfa))!
            .GetCustomAttributes<HttpPostAttribute>()
            .Select(attribute => attribute.Template);

        Assert.Contains("login/{product:regex(coaching|speed-reading|course-management)}", loginRoutes);
        Assert.Contains("mfa/verify", mfaRoutes);
        Assert.Contains("mfa/setup", mfaSetupRoutes);
        Assert.Contains("mfa/enable", mfaEnableRoutes);
    }

    private static CourseManagementAuthController CreateController(IMediator mediator, DefaultHttpContext? context = null)
    {
        var controller = new CourseManagementAuthController(mediator, new TestWebHostEnvironment());
        controller.ControllerContext = new ControllerContext { HttpContext = context ?? new DefaultHttpContext() };
        return controller;
    }

    private class CapturingMediatorProxy : DispatchProxy
    {
        public object? Request { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != "Send") throw new InvalidOperationException("Unexpected mediator call.");
            Request = args?.FirstOrDefault(argument => argument is not CancellationToken);
            return Request switch
            {
                LoginCommand => Task.FromResult(Result.Success(new LoginResponse("access-token", "course-refresh-token", DateTime.UtcNow.AddDays(1), true))),
                RefreshTokenCommand => Task.FromResult(Result.Success(new RefreshTokenResponse("refreshed-access", "next-course-refresh", DateTime.UtcNow.AddDays(1), true))),
                OpenCourseManagementSessionCommand => Task.FromResult(Result.Success(new LoginResponse("course-access", "new-course-refresh", DateTime.UtcNow.AddDays(1), false))),
                RevokeTokenCommand => Task.FromResult(Result.Success()),
                _ => throw new InvalidOperationException($"Unexpected request: {Request?.GetType().Name}")
            };
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
