using FluentAssertions;
using Identity.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class MfaEndpointSecurityTests
{
    [Theory]
    [InlineData(nameof(AuthController.StartMfaSetup), "mfa/setup")]
    [InlineData(nameof(AuthController.EnableMfa), "mfa/enable")]
    [InlineData(nameof(AuthController.VerifyMfa), "mfa/verify")]
    public void FirstFactorMfaEndpoints_ShouldBeAnonymousAndExplicitlyRouted(
        string actionName,
        string route)
    {
        var action = typeof(AuthController).GetMethod(actionName);

        action.Should().NotBeNull();
        action!.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Should().ContainSingle();
        action.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true)
            .Cast<HttpPostAttribute>()
            .Should().ContainSingle(attribute => attribute.Template == route);
    }
}
