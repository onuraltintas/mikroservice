using FluentAssertions;
using Identity.Application.Commands.ManageInstitutions;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Authorization;
using System.Security.Claims;

namespace Identity.API.IntegrationTests;

public sealed class InstitutionManagementTests
{
    [Fact]
    public async Task InstitutionRepository_ReturnsCountsAndFiltersByActiveState()
    {
        await using var context = CreateContext();
        var institution = Institution.Create("Test School", InstitutionType.School, "Istanbul", "school@test.local");
        var user = User.Create(Guid.NewGuid(), "student@test.local", "Test", "Student");
        var student = StudentProfile.Create(user.Id, "Test", "Student", institution.Id);
        context.AddRange(institution, user, student);
        await context.SaveChangesAsync();

        var repository = new InstitutionRepository(context);
        var result = await repository.GetAllAsync(1, 25, "school", true, null, CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(item => item.StudentCount == 1 && item.TeacherCount == 0);
    }

    [Fact]
    public async Task SetInstitutionActiveCommand_ChangesTenantAvailability()
    {
        await using var context = CreateContext();
        var institution = Institution.Create("Test School", InstitutionType.School);
        context.Institutions.Add(institution);
        await context.SaveChangesAsync();

        var handler = new SetInstitutionActiveCommandHandler(
            new InstitutionRepository(context),
            new UserRepository(context),
            new UnitOfWork(context),
            new InstitutionManagementAuthorization(
                new StubCurrentUserService(Guid.NewGuid(), "SystemAdmin"),
                new InstitutionRepository(context)));

        var result = await handler.Handle(new SetInstitutionActiveCommand(institution.Id, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.Institutions.FindAsync(institution.Id))!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivatingInstitution_RevokesAllMemberRefreshSessions()
    {
        await using var context = CreateContext();
        var institution = Institution.Create("Security Test School", InstitutionType.School);
        var administrator = User.Create(Guid.NewGuid(), "admin@security.test", "Tenant", "Admin");
        var teacher = User.Create(Guid.NewGuid(), "teacher@security.test", "Tenant", "Teacher");
        var student = User.Create(Guid.NewGuid(), "student@security.test", "Tenant", "Student");
        var parent = User.Create(Guid.NewGuid(), "parent@security.test", "Tenant", "Parent");
        var unrelatedUser = User.Create(Guid.NewGuid(), "unrelated@security.test", "Other", "Tenant");
        var adminRole = Role.Create(Identity.Domain.Enums.UserRole.InstitutionAdmin.ToString(), "Institution admin", isSystemRole: true);

        administrator.AddRole(new Identity.Domain.Entities.UserRole(administrator.Id, adminRole.Id));
        teacher.AddRefreshToken(RefreshToken.Create(teacher.Id, "tenant-teacher-refresh", DateTime.UtcNow.AddDays(1), "127.0.0.1"));
        student.AddRefreshToken(RefreshToken.Create(student.Id, "tenant-student-refresh", DateTime.UtcNow.AddDays(1), "127.0.0.1"));
        parent.AddRefreshToken(RefreshToken.Create(parent.Id, "tenant-parent-refresh", DateTime.UtcNow.AddDays(1), "127.0.0.1"));
        administrator.AddRefreshToken(RefreshToken.Create(administrator.Id, "tenant-admin-refresh", DateTime.UtcNow.AddDays(1), "127.0.0.1"));
        var unrelatedToken = RefreshToken.Create(unrelatedUser.Id, "unrelated-refresh", DateTime.UtcNow.AddDays(1), "127.0.0.1");
        unrelatedUser.AddRefreshToken(unrelatedToken);

        context.AddRange(
            institution,
            adminRole,
            administrator,
            teacher,
            student,
            parent,
            unrelatedUser,
            InstitutionAdmin.Create(administrator.Id, institution.Id, InstitutionAdminRole.Admin),
            TeacherProfile.Create(teacher.Id, "Tenant", "Teacher", institution.Id),
            StudentProfile.Create(student.Id, "Tenant", "Student", institution.Id, parent.Id));
        await context.SaveChangesAsync();

        var repository = new InstitutionRepository(context);
        var result = await new SetInstitutionActiveCommandHandler(
                repository,
                new UserRepository(context),
                new UnitOfWork(context),
                new InstitutionManagementAuthorization(
                    new StubCurrentUserService(Guid.NewGuid(), "SystemAdmin"),
                    repository))
            .Handle(new SetInstitutionActiveCommand(institution.Id, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.RefreshTokens.Where(token => token.UserId != unrelatedUser.Id).ToListAsync())
            .Should().OnlyContain(token => token.IsRevoked);
        (await context.RefreshTokens.SingleAsync(token => token.Id == unrelatedToken.Id)).IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task AssignAdminCommand_RejectsUserWithoutInstitutionAdminRole()
    {
        await using var context = CreateContext();
        var institution = Institution.Create("Test School", InstitutionType.School);
        var role = Role.Create(Identity.Domain.Enums.UserRole.Student.ToString(), "Student role", isSystemRole: true);
        var user = User.Create(Guid.NewGuid(), "student@test.local", "Test", "Student");
        user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id));
        context.AddRange(institution, role, user);
        await context.SaveChangesAsync();

        var handler = new AssignInstitutionAdminCommandHandler(
            new InstitutionRepository(context),
            new UserRepository(context),
            new UnitOfWork(context),
            new InstitutionManagementAuthorization(
                new StubCurrentUserService(Guid.NewGuid(), "SystemAdmin"),
                new InstitutionRepository(context)));

        var result = await handler.Handle(
            new AssignInstitutionAdminCommand(institution.Id, user.Id, InstitutionAdminRole.Admin),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.InvalidRole");
    }

    [Fact]
    public async Task InstitutionAdminLifecycle_AssignsAndDeactivatesManager()
    {
        await using var context = CreateContext();
        var institution = Institution.Create("Test School", InstitutionType.School);
        var role = Role.Create(Identity.Domain.Enums.UserRole.InstitutionAdmin.ToString(), "Institution admin", isSystemRole: true);
        var user = User.Create(Guid.NewGuid(), "manager@test.local", "Test", "Manager");
        user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id));
        var refreshToken = RefreshToken.Create(user.Id, "institution-admin-refresh", DateTime.UtcNow.AddDays(1), "127.0.0.1");
        user.AddRefreshToken(refreshToken);
        context.AddRange(institution, role, user);
        await context.SaveChangesAsync();

        var repository = new InstitutionRepository(context);
        var unitOfWork = new UnitOfWork(context);
        var assignResult = await new AssignInstitutionAdminCommandHandler(
            repository,
            new UserRepository(context),
            unitOfWork,
            new InstitutionManagementAuthorization(
                new StubCurrentUserService(Guid.NewGuid(), "SystemAdmin"),
                repository)).Handle(
                new AssignInstitutionAdminCommand(institution.Id, user.Id, InstitutionAdminRole.Admin),
                CancellationToken.None);

        assignResult.IsSuccess.Should().BeTrue();
        var deactivateResult = await new SetInstitutionAdminActiveCommandHandler(
                repository,
                new UserRepository(context),
                unitOfWork,
                new InstitutionManagementAuthorization(
                    new StubCurrentUserService(Guid.NewGuid(), "SystemAdmin"),
                    repository))
            .Handle(new SetInstitutionAdminActiveCommand(institution.Id, user.Id, false), CancellationToken.None);

        deactivateResult.IsSuccess.Should().BeTrue();
        (await repository.GetAdminsAsync(institution.Id, CancellationToken.None))
            .Should().ContainSingle(item => item.UserId == user.Id && !item.IsActive);
        (await context.RefreshTokens.SingleAsync(token => token.Id == refreshToken.Id)).IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task AssigningDeactivatedAdmin_ReactivatesExistingMembershipAndUpdatesRole()
    {
        await using var context = CreateContext();
        var institution = Institution.Create("Reactivation School", InstitutionType.School);
        var role = Role.Create(Identity.Domain.Enums.UserRole.InstitutionAdmin.ToString(), "Institution admin", isSystemRole: true);
        var user = User.Create(Guid.NewGuid(), "reactivate@test.local", "Reactivation", "Manager");
        user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id));
        var membership = InstitutionAdmin.Create(user.Id, institution.Id, InstitutionAdminRole.Admin);
        membership.Deactivate();
        context.AddRange(institution, role, user, membership);
        await context.SaveChangesAsync();

        var repository = new InstitutionRepository(context);
        var result = await new AssignInstitutionAdminCommandHandler(
                repository,
                new UserRepository(context),
                new UnitOfWork(context),
                new InstitutionManagementAuthorization(
                    new StubCurrentUserService(Guid.NewGuid(), "SystemAdmin"),
                    repository))
            .Handle(new AssignInstitutionAdminCommand(institution.Id, user.Id, InstitutionAdminRole.Owner), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var admins = await repository.GetAdminsAsync(institution.Id, CancellationToken.None);
        admins.Should().HaveCount(1);
        admins.Should().ContainSingle(item =>
            item.UserId == user.Id
            && item.IsActive
            && item.Role == InstitutionAdminRole.Owner);
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
