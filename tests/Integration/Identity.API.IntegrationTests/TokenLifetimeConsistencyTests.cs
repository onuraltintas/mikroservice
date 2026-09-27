using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Identity.Application.DTOs.Settings;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using PlatformProduct = Identity.Domain.Enums.PlatformProduct;
using Identity.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

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

    [Theory]
    [InlineData(PlatformProduct.Coaching, "coaching")]
    [InlineData(PlatformProduct.SpeedReading, "speed-reading")]
    public async Task ProductSessionToken_ShouldContainCanonicalProductClaim(
        PlatformProduct product,
        string expectedProduct)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = "a-secure-test-secret-that-is-at-least-32-characters",
            ["JWT_ISSUER"] = "test-issuer",
            ["JWT_AUDIENCE"] = "test-audience"
        }).Build();
        var service = new TokenService(configuration, new StubConfigurationService());

        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            await service.GenerateAccessTokenAsync(
                User.Create(Guid.NewGuid(), "user@example.test"),
                product: product));

        token.Claims.Should().ContainSingle(claim =>
            claim.Type == "platform_product" && claim.Value == expectedProduct);
    }

    [Fact]
    public async Task ProductSessionToken_ShouldContainOnlyRolesGrantedForThatProduct()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = "a-secure-test-secret-that-is-at-least-32-characters",
            ["JWT_ISSUER"] = "test-issuer",
            ["JWT_AUDIENCE"] = "test-audience"
        }).Build();
        var user = User.Create(Guid.NewGuid(), "multi-platform@example.test");
        var coachingRole = Role.Create("Teacher", "Coaching teacher");
        var speedReadingRole = Role.Create("Student", "Speed-reading student");
        var legacyRole = Role.Create("Editor", "Legacy unscoped role");
        AddRole(user, coachingRole, PlatformProduct.Coaching);
        AddRole(user, speedReadingRole, PlatformProduct.SpeedReading);
        AddRole(user, legacyRole, product: null);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            await new TokenService(configuration, new StubConfigurationService())
                .GenerateAccessTokenAsync(user, product: PlatformProduct.Coaching));

        token.Claims
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Should()
            .BeEquivalentTo(["Teacher"]);
    }

    [Fact]
    public async Task ProductSessionToken_ShouldNotCarryPermissionsFromTheOtherPlatform()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = "a-secure-test-secret-that-is-at-least-32-characters",
            ["JWT_ISSUER"] = "test-issuer",
            ["JWT_AUDIENCE"] = "test-audience"
        }).Build();
        var user = User.Create(Guid.NewGuid(), "teacher@example.test");
        var role = Role.Create("Teacher", "Coaching teacher");
        role.Permissions.Add(new RolePermission(role.Id, Identity.Domain.Constants.Permissions.Coaching.View));
        role.Permissions.Add(new RolePermission(role.Id, Identity.Domain.Constants.Permissions.SpeedReading.ReportView));
        AddRole(user, role, PlatformProduct.Coaching);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            await new TokenService(configuration, new StubConfigurationService())
                .GenerateAccessTokenAsync(user, product: PlatformProduct.Coaching));

        var permissions = token.Claims
            .Where(claim => claim.Type == "permission")
            .Select(claim => claim.Value)
            .ToArray();
        permissions.Should().Contain(Identity.Domain.Constants.Permissions.Coaching.View);
        permissions.Should().NotContain(Identity.Domain.Constants.Permissions.SpeedReading.ReportView);
    }

    [Fact]
    public async Task ProductSessionToken_ShouldRetainGlobalSystemAdminButExcludeLegacyGlobalBusinessRoles()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = "a-secure-test-secret-that-is-at-least-32-characters",
            ["JWT_ISSUER"] = "test-issuer",
            ["JWT_AUDIENCE"] = "test-audience"
        }).Build();
        var user = User.Create(Guid.NewGuid(), "admin@example.test");
        var systemAdminRole = Role.Create("SystemAdmin", "System administrator", isSystemRole: true);
        var legacyTeacherRole = Role.Create("Teacher", "Legacy unscoped role");
        AddRole(user, systemAdminRole, product: null);
        AddRole(user, legacyTeacherRole, product: null);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            await new TokenService(configuration, new StubConfigurationService())
                .GenerateAccessTokenAsync(user, product: PlatformProduct.Coaching));

        token.Claims
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Should()
            .ContainSingle(role => role == "SystemAdmin");
    }

    private static void AddRole(User user, Role role, PlatformProduct? product)
    {
        var userRole = new UserRole(user.Id, role.Id, product);
        typeof(UserRole).GetProperty(nameof(UserRole.Role))!.SetValue(userRole, role);
        user.AddRole(userRole);
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
    public async Task SystemAdministratorToken_ShouldIncludeAssignedPermissionsBeyondCompiledCatalog()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = "a-secure-test-secret-that-is-at-least-32-characters",
            ["JWT_ISSUER"] = "test-issuer",
            ["JWT_AUDIENCE"] = "test-audience"
        }).Build();
        var user = User.Create(Guid.NewGuid(), "admin@example.test");
        var role = Role.Create("SystemAdmin", "System administrator", isSystemRole: true);
        role.Permissions.Add(new RolePermission(role.Id, "Permissions.Privacy.FutureManage"));
        var userRole = new UserRole(user.Id, role.Id);
        typeof(UserRole).GetProperty(nameof(UserRole.Role))!.SetValue(userRole, role);
        user.AddRole(userRole);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            await new TokenService(configuration, new StubConfigurationService())
                .GenerateAccessTokenAsync(user));

        token.Claims.Should().Contain(claim =>
            claim.Type == "permission" && claim.Value == "Permissions.Privacy.FutureManage");
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
