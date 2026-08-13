using EduPlatform.Shared.Security.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Identity.API.IntegrationTests;

public class InternalServiceAuthenticationTests
{
    private const string ValidKey = "test-service-key-with-at-least-32-bytes";

    [Fact]
    public void MatchingServiceKey_ShouldBeAccepted()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[InternalServiceAuthentication.HeaderName] = ValidKey;
        var configuration = CreateConfiguration(ValidKey);

        InternalServiceAuthentication.IsValid(context.Request, configuration).Should().BeTrue();
    }

    [Fact]
    public void WrongServiceKey_ShouldBeRejected()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[InternalServiceAuthentication.HeaderName] = "wrong-key";
        var configuration = CreateConfiguration(ValidKey);

        InternalServiceAuthentication.IsValid(context.Request, configuration).Should().BeFalse();
    }

    [Fact]
    public void MissingConfiguredKey_ShouldBeRejected()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[InternalServiceAuthentication.HeaderName] = ValidKey;
        var configuration = CreateConfiguration(null);

        InternalServiceAuthentication.IsValid(context.Request, configuration).Should().BeFalse();
    }

    [Fact]
    public void ShortConfiguredKey_ShouldBeRejected()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[InternalServiceAuthentication.HeaderName] = "short-key";
        var configuration = CreateConfiguration("short-key");

        InternalServiceAuthentication.IsValid(context.Request, configuration).Should().BeFalse();
    }

    [Fact]
    public void ValidConfiguredKey_ShouldPassStartupValidation()
    {
        var configuration = CreateConfiguration(ValidKey);

        var action = () => InternalServiceAuthentication.ValidateConfiguration(configuration);

        action.Should().NotThrow();
    }

    [Fact]
    public void ShortConfiguredKey_ShouldFailStartupValidation()
    {
        var configuration = CreateConfiguration("short-key");

        var action = () => InternalServiceAuthentication.ValidateConfiguration(configuration);

        action.Should().Throw<InvalidOperationException>();
    }

    private static IConfiguration CreateConfiguration(string? key)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Internal:ServiceApiKey"] = key
            })
            .Build();
    }
}
