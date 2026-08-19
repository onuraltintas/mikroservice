using Identity.API.Controllers;
using Identity.Application.Commands.ConfirmEmail;
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
}
