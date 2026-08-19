using EduPlatform.Shared.Infrastructure.Logging;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Identity.API.IntegrationTests;

public sealed class RequestLoggingTests
{
    [Theory]
    [InlineData("/api/users/938b7d2d-450a-4a31-9b7f-8f54a4b6c19e", "/api/users/{id}")]
    [InlineData("/api/users/12345/profile", "/api/users/{id}/profile")]
    public void CreateSafePath_MasksResourceIdentifiers(string path, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        SerilogConfiguration.CreateSafePath(context).Should().Be(expected);
    }
}
