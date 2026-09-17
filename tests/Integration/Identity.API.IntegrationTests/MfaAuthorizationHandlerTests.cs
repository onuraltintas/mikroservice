using System.Security.Claims;
using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using EduPlatform.Shared.Security.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

public sealed class MfaAuthorizationHandlerTests
{
    [Theory]
    [InlineData(MfaPolicyModes.Required, "GET", false)]
    [InlineData(MfaPolicyModes.Required, "POST", false)]
    [InlineData(MfaPolicyModes.Required, "POST", true)]
    [InlineData(MfaPolicyModes.MutationsOnly, "GET", true)]
    [InlineData(MfaPolicyModes.MutationsOnly, "POST", false)]
    [InlineData(MfaPolicyModes.MutationsOnly, "POST", true)]
    [InlineData(MfaPolicyModes.Disabled, "GET", true)]
    [InlineData(MfaPolicyModes.Disabled, "POST", true)]
    public async Task CategoryMode_ShouldControlMfaRequirement(
        string mode,
        string method,
        bool hasMfaClaim)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new MfaCategoryAttribute(MfaOperationCategories.Users)),
            "mfa-test"));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        if (hasMfaClaim)
        {
            claims.Add(new Claim("amr", "mfa"));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var requirement = new MfaPolicyRequirement();
        var context = new AuthorizationHandlerContext([requirement], principal, httpContext);
        var handler = new MfaAuthorizationHandler(new StubMfaPolicyStore(mode));

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().Be(
            mode.Equals(MfaPolicyModes.Disabled, StringComparison.OrdinalIgnoreCase)
            || (mode.Equals(MfaPolicyModes.MutationsOnly, StringComparison.OrdinalIgnoreCase)
                && method == "GET")
            || hasMfaClaim);
    }

    [Theory]
    [InlineData(MfaPolicyModes.Required, false)]
    [InlineData(MfaPolicyModes.Required, true)]
    [InlineData(MfaPolicyModes.Disabled, true)]
    public async Task LegacyRequirement_ShouldUseSystemCategory(
        string mode,
        bool hasMfaClaim)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        var claims = hasMfaClaim
            ? new[] { new Claim("amr", "mfa") }
            : Array.Empty<Claim>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var requirement = new MfaPolicyRequirement();
        var context = new AuthorizationHandlerContext([requirement], principal, httpContext);
        var handler = new MfaAuthorizationHandler(new StubMfaPolicyStore(mode));

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().Be(
            mode.Equals(MfaPolicyModes.Disabled, StringComparison.OrdinalIgnoreCase)
            || hasMfaClaim);
    }

    [Fact]
    public async Task ManagementPermissionPolicy_ShouldCarryItsMfaCategory()
    {
        var services = new ServiceCollection();
        services.AddCustomAuthorization();
        await using var provider = services.BuildServiceProvider();

        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync("Permissions.Users.Edit");

        policy.Should().NotBeNull();
        policy!.Requirements
            .OfType<MfaPolicyRequirement>()
            .Should()
            .ContainSingle(requirement =>
                requirement.Category == MfaOperationCategories.Users);
    }

    private sealed class StubMfaPolicyStore(string mode) : IMfaPolicyStore
    {
        public Task<string> GetModeAsync(string category, CancellationToken cancellationToken = default) =>
            Task.FromResult(mode);
    }
}
