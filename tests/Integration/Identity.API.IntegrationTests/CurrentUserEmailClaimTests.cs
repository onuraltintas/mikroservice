using System.Security.Claims;
using EduPlatform.Shared.Security.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Identity.API.IntegrationTests;

public sealed class CurrentUserEmailClaimTests
{
    [Fact]
    public void ReadsEmailFromUnmappedJwtClaim()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("email", "institution@example.com")], "Bearer"))
        };
        var service = new CurrentUserService(new HttpContextAccessor { HttpContext = context });

        service.Email.Should().Be("institution@example.com");
    }

    [Fact]
    public void StillReadsMappedEmailClaim()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Email, "teacher@example.com")], "Bearer"))
        };
        var service = new CurrentUserService(new HttpContextAccessor { HttpContext = context });

        service.Email.Should().Be("teacher@example.com");
    }
}
