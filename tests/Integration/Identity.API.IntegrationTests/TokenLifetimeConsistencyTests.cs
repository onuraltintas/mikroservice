using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Identity.Application.DTOs.Settings;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace Identity.API.IntegrationTests;

public sealed class TokenLifetimeConsistencyTests
{
    [Fact]
    public void ReportedLifetime_ShouldMatchGeneratedJwtExpiration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = "a-secure-test-secret-that-is-at-least-32-characters",
            ["JWT_ISSUER"] = "test-issuer",
            ["JWT_AUDIENCE"] = "test-audience",
            ["JWT_EXPIRY_MINUTES"] = "42"
        }).Build();
        var service = new TokenService(configuration, new StubConfigurationService());

        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            service.GenerateAccessToken(User.Create(Guid.NewGuid(), "user@example.test")));

        service.GetAccessTokenLifetimeMinutes().Should().Be(42);
        (token.ValidTo - token.ValidFrom).Should().Be(TimeSpan.FromMinutes(42));
    }

    private sealed class StubConfigurationService : IConfigurationService
    {
        public Task<string?> GetConfigurationValueAsync(string key, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<List<ConfigurationDto>> GetAllConfigurationsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetManageableConfigurationValueAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetPublicConfigurationValueAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConfigurationDto> CreateConfigurationAsync(CreateConfigurationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateConfigurationAsync(string key, UpdateConfigurationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteConfigurationAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RefreshCacheAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
