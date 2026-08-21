using Identity.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class GoogleAuthServiceTests
{
    [Fact]
    public async Task VerifyGoogleTokenAsync_RejectsWhenClientIdIsNotConfigured()
    {
        var service = CreateService();

        var result = await service.VerifyGoogleTokenAsync("eyJhbGciOiJSUzI1NiJ9.invalid.signature");

        result.Should().BeNull();
    }

    [Fact]
    public async Task VerifyGoogleTokenAsync_RejectsOversizedTokensBeforeVerification()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["GOOGLE_CLIENT_ID"] = "test-client.apps.googleusercontent.com"
        });

        var result = await service.VerifyGoogleTokenAsync(new string('x', 16_385));

        result.Should().BeNull();
    }

    [Fact]
    public async Task VerifyGoogleTokenAsync_RejectsMalformedTokensWithoutThrowing()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["GOOGLE_CLIENT_ID"] = "test-client.apps.googleusercontent.com"
        });

        var result = await service.VerifyGoogleTokenAsync("not-a-jwt");

        result.Should().BeNull();
    }

    private static GoogleAuthService CreateService(
        IDictionary<string, string?>? values = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();

        return new GoogleAuthService(configuration, NullLogger<GoogleAuthService>.Instance);
    }
}
