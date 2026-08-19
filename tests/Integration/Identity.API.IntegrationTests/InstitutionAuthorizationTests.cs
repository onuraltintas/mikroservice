using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Authorization;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class InstitutionAuthorizationTests
{
    [Fact]
    public async Task InstitutionAdmin_CannotAccessAnotherInstitution()
    {
        await using var context = CreateContext();
        var ownInstitution = Institution.Create("Own", InstitutionType.School);
        var otherInstitution = Institution.Create("Other", InstitutionType.School);
        var admin = User.Create(Guid.NewGuid(), "admin@test.local", "Tenant", "Admin");
        context.AddRange(
            ownInstitution,
            otherInstitution,
            admin,
            InstitutionAdmin.Create(admin.Id, ownInstitution.Id, InstitutionAdminRole.Admin));
        await context.SaveChangesAsync();

        var authorization = new InstitutionManagementAuthorization(
            new StubCurrentUserService(admin.Id, "InstitutionAdmin"),
            new InstitutionRepository(context));

        (await authorization.EnsureInstitutionAccessAsync(ownInstitution.Id, CancellationToken.None))
            .IsSuccess.Should().BeTrue();
        (await authorization.EnsureInstitutionAccessAsync(otherInstitution.Id, CancellationToken.None))
            .Error.Code.Should().Be("Error.Forbidden");
        authorization.EnsureSystemAdministrator().Error.Code.Should().Be("Error.Forbidden");
    }

    [Fact]
    public async Task SystemAdmin_HasGlobalInstitutionScope()
    {
        await using var context = CreateContext();
        var repository = new InstitutionRepository(context);
        var authorization = new InstitutionManagementAuthorization(
            new StubCurrentUserService(Guid.NewGuid(), "SystemAdmin"),
            repository);

        var scope = await authorization.ResolveScopeAsync(CancellationToken.None);

        scope.IsSuccess.Should().BeTrue();
        scope.Value.IsGlobal.Should().BeTrue();
        authorization.EnsureSystemAdministrator().IsSuccess.Should().BeTrue();
    }

    private static IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        private readonly Guid _userId;
        private readonly string[] _roles;

        public StubCurrentUserService(Guid userId, params string[] roles)
        {
            _userId = userId;
            _roles = roles;
        }

        public Guid? UserId => _userId;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => _roles;
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
