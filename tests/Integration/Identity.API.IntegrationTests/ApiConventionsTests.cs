using EduPlatform.Shared.Infrastructure.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

public sealed class ApiConventionsTests
{
    [Fact]
    public void InvalidModelState_ShouldUseProblemDetailsContract()
    {
        var services = new ServiceCollection();
        services.AddControllers().AddEduPlatformApiConventions();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiBehaviorOptions>>().Value;
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "trace-validation-123";
        httpContext.Request.Path = "/api/auth/login";

        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Email", "Email is required.");
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            modelState);

        var result = options.InvalidModelStateResponseFactory(actionContext);
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.ContentTypes.Should().ContainSingle("application/problem+json");

        var problem = badRequest.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Type.Should().Be("https://eduplatform.dev/problems/validation-error");
        problem.Instance.Should().Be("/api/auth/login");
        problem.Extensions["traceId"].Should().Be("trace-validation-123");
        problem.Errors["Email"].Should().ContainSingle("Email is required.");
    }
}
