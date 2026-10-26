using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Identity.Application.DTOs.Settings;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace Identity.API.IntegrationTests;

public sealed class TokenLifetimeConsistencyTests
{
    [Fact]
    public async Task ReportedLifetime_ShouldMatchGeneratedJwtExpiration()
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
            await service.GenerateAccessTokenAsync(User.Create(Guid.NewGuid(), "user@example.test")));

        (await service.GetAccessTokenLifetimeMinutesAsync()).Should().Be(42);
        (token.ValidTo - token.ValidFrom).Should().Be(TimeSpan.FromMinutes(42));
    }

    [Fact]
    public async Task SystemAdministratorToken_ShouldContainAllPlatformPermissions_WhenRoleNavigationHasNoPermissions()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = "a-secure-test-secret-that-is-at-least-32-characters",
            ["JWT_ISSUER"] = "test-issuer",
            ["JWT_AUDIENCE"] = "test-audience"
        }).Build();
        var service = new TokenService(configuration, new StubConfigurationService());
        var user = User.Create(Guid.NewGuid(), "admin@example.test");
        var role = Role.Create("SystemAdmin", "System administrator", isSystemRole: true);
        var userRole = new UserRole(user.Id, role.Id);
        typeof(UserRole).GetProperty(nameof(UserRole.Role))!.SetValue(userRole, role);
        user.AddRole(userRole);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            await service.GenerateAccessTokenAsync(user));

        var permissions = token.Claims
            .Where(claim => claim.Type == "permission")
            .Select(claim => claim.Value);
        permissions.Should().BeEquivalentTo(Identity.Domain.Constants.Permissions.GetAll());
    }

    [Fact]
    public async Task ProductSessionToken_ShouldContainOnlyRolesForItsProduct()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = "a-secure-test-secret-that-is-at-least-32-characters",
            ["JWT_ISSUER"] = "test-issuer",
            ["JWT_AUDIENCE"] = "test-audience"
        }).Build();
        var service = new TokenService(configuration, new StubConfigurationService());
        var user = User.Create(Guid.NewGuid(), "multi-product@example.test");
        AddRole(user, "Student", Identity.Domain.Enums.PlatformProduct.Coaching);
        AddRole(user, "Teacher", Identity.Domain.Enums.PlatformProduct.SpeedReading);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            await service.GenerateAccessTokenAsync(user, product: Identity.Domain.Enums.PlatformProduct.Coaching));

        token.Claims.Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Should().Equal("Student");
        token.Claims.Single(claim => claim.Type == "platform_product")
            .Value.Should().Be("coaching");
    }

    private static void AddRole(User user, string roleName, Identity.Domain.Enums.PlatformProduct product)
    {
        var role = Role.Create(roleName, roleName);
        var userRole = new UserRole(user.Id, role.Id, product);
        typeof(UserRole).GetProperty(nameof(UserRole.Role))!.SetValue(userRole, role);
        user.AddRole(userRole);
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
