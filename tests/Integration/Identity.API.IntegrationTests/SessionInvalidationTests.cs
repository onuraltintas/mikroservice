using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using EduPlatform.Shared.Security.Services;
using FluentAssertions;
using Identity.Application.Interfaces;
using Identity.Application.Commands.ChangePassword;
using Identity.Application.Commands.ResetPassword;
using Identity.Application.Commands.CreateUser;
using Identity.API.Controllers;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Mvc;
using MassTransit;

namespace Identity.API.IntegrationTests;

public sealed class SessionInvalidationTests
{
    [Fact]
    public async Task Admin_creating_parent_user_also_creates_parent_profile()
    {
        await using var context = CreateContext();
        context.Roles.Add(Role.Create("Parent", "Parent"));
        await context.SaveChangesAsync();
        var bus = Bus.Factory.CreateUsingInMemory(_ => { });
        await bus.StartAsync();
        try
        {
            var handler = new CreateUserCommandHandler(
                CreateService(context), new UserRepository(context),
                new StudentRepository(context), new TeacherRepository(context),
                new ParentRepository(context),
                new InstitutionRepository(context), new UnitOfWork(context),
                bus, new SystemAdminCurrentUser(),
                NullLogger<CreateUserCommandHandler>.Instance);

            var result = await handler.Handle(new CreateUserCommand(
                "Parent", "One", "parent@example.test", null, "Parent"), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            context.ParentProfiles.Should().ContainSingle(profile => profile.UserId == result.Value.UserId);
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    [Fact]
    public async Task Failed_bulk_role_assignment_does_not_remove_existing_roles()
    {
        await using var context = CreateContext();
        var user = User.Create(Guid.NewGuid(), "member@example.test", "Member", "One");
        var teacherRole = Role.Create("Teacher", "Teacher");
        var institutionRole = Role.Create("InstitutionAdmin", "Institution admin");
        user.AddRole(new UserRole(user.Id, teacherRole.Id));
        context.AddRange(user, teacherRole, institutionRole);
        await context.SaveChangesAsync();
        var controller = new BulkUsersController(
            null!, CreateService(context), new UserRepository(context),
            NullLogger<BulkUsersController>.Instance);

        var response = await controller.AssignRole(
            new BulkUsersController.BulkRoleAssignmentRequest(
                [user.Id], institutionRole.Name, RemoveExistingRoles: true),
            CancellationToken.None);

        response.Should().BeOfType<BadRequestObjectResult>();
        context.UserRoles.Should().ContainSingle(assignment =>
            assignment.UserId == user.Id && assignment.RoleId == teacherRole.Id);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    [InlineData("Parent")]
    public async Task Admin_role_assignment_creates_missing_profile_without_removing_existing_student_profile(string roleName)
    {
        await using var context = CreateContext();
        var user = User.Create(Guid.NewGuid(), "member@example.test", "Member", "One");
        var role = Role.Create(roleName, roleName);
        context.AddRange(user, role);
        if (roleName != "Student")
            context.StudentProfiles.Add(StudentProfile.Create(user.Id, "Member", "One"));
        await context.SaveChangesAsync();

        var result = await CreateService(context).AssignRoleWithProfileAsync(
            user.Id, roleName, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.StudentProfiles.Should().ContainSingle(profile => profile.UserId == user.Id);
        if (roleName == "Teacher")
            context.TeacherProfiles.Should().ContainSingle(profile => profile.UserId == user.Id);
        if (roleName == "Parent")
            context.ParentProfiles.Should().ContainSingle(profile => profile.UserId == user.Id);
        (await context.UserRoles.CountAsync(assignment => assignment.UserId == user.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Admin_role_assignment_repairs_profile_without_duplicating_existing_role()
    {
        await using var context = CreateContext();
        var user = User.Create(Guid.NewGuid(), "teacher@example.test", "Teacher", "One");
        var role = Role.Create("Teacher", "Teacher");
        user.AddRole(new UserRole(user.Id, role.Id));
        context.AddRange(user, role);
        await context.SaveChangesAsync();

        var result = await CreateService(context).AssignRoleWithProfileAsync(
            user.Id, role.Name, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.TeacherProfiles.Should().ContainSingle(profile => profile.UserId == user.Id);
        context.UserRoles.Should().ContainSingle(assignment => assignment.UserId == user.Id);
    }

    [Theory]
    [InlineData("InstitutionAdmin")]
    [InlineData("InstitutionOwner")]
    public async Task Admin_role_assignment_requires_institution_management_for_institution_roles(string roleName)
    {
        await using var context = CreateContext();
        var user = User.Create(Guid.NewGuid(), "member@example.test", "Member", "One");
        var role = Role.Create(roleName, roleName);
        context.AddRange(user, role);
        await context.SaveChangesAsync();

        var result = await CreateService(context).AssignRoleWithProfileAsync(
            user.Id, roleName, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        context.UserRoles.Should().BeEmpty();
        context.InstitutionAdmins.Should().BeEmpty();
    }

    [Theory]
    [InlineData("reset-password")]
    [InlineData("deactivate")]
    [InlineData("assign-role")]
    [InlineData("remove-role")]
    public async Task SecuritySensitiveUserChange_ShouldRevokeActiveRefreshTokens(string operation)
    {
        await using var context = CreateContext();
        var user = User.Create(Guid.NewGuid(), "user@example.test", "Test", "User");
        var role = Role.Create("Teacher", "Teacher role");
        var refreshToken = RefreshToken.Create(
            user.Id,
            $"refresh-{Guid.NewGuid():N}",
            DateTime.UtcNow.AddDays(7),
            "127.0.0.1");
        user.AddRefreshToken(refreshToken);

        if (operation == "remove-role")
        {
            user.AddRole(new UserRole(user.Id, role.Id));
        }

        context.Users.Add(user);
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = operation switch
        {
            "reset-password" => await service.ResetPasswordAsync(
                user.Id, "Replacement-Password-1!", CancellationToken.None),
            "deactivate" => await service.DeactivateUserAsync(user.Id, CancellationToken.None),
            "assign-role" => await service.AssignRoleAsync(user.Id, role.Name, CancellationToken.None),
            "remove-role" => await service.RemoveRoleAsync(user.Id, role.Name, CancellationToken.None),
            _ => throw new InvalidOperationException($"Unknown operation: {operation}")
        };

        result.IsSuccess.Should().BeTrue();
        var storedToken = await context.RefreshTokens
            .AsNoTracking()
            .SingleAsync(token => token.Id == refreshToken.Id);
        storedToken.IsRevoked.Should().BeTrue();
        storedToken.ReasonRevoked.Should().Contain("security-sensitive");
    }

    [Fact]
    public async Task UserChangingOwnPassword_ShouldRevokeActiveRefreshTokens()
    {
        await using var context = CreateContext();
        var hasher = new PasswordHasher();
        var user = User.Create(Guid.NewGuid(), "self@example.test", "Self", "User");
        hasher.CreatePasswordHash("Current-Password-1!", out var hash, out var salt);
        user.SetPassword(hash, salt);
        var refreshToken = AddRefreshToken(user);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);
        var handler = new ChangePasswordCommandHandler(
            repository,
            hasher,
            new UnitOfWork(context),
            new SystemAdminCurrentUser(user.Id));

        var result = await handler.Handle(
            new ChangePasswordCommand("Current-Password-1!", "Replacement-Password-1!"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await ReadTokenAsync(context, refreshToken.Id)).IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task PasswordReset_ShouldRevokeActiveRefreshTokens()
    {
        await using var context = CreateContext();
        var user = User.Create(Guid.NewGuid(), "reset@example.test", "Reset", "User");
        user.GeneratePasswordResetToken();
        var resetToken = user.PasswordResetToken!;
        var refreshToken = AddRefreshToken(user);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);
        var handler = new ResetPasswordCommandHandler(
            repository,
            new UnitOfWork(context),
            new PasswordHasher());

        var result = await handler.Handle(
            new ResetPasswordCommand(
                user.Email,
                resetToken,
                "Replacement-Password-1!"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await ReadTokenAsync(context, refreshToken.Id)).IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void RefreshToken_ShouldRememberBrowserPersistencePreference()
    {
        var token = RefreshToken.Create(
            Guid.NewGuid(),
            "session-token",
            DateTime.UtcNow.AddDays(1),
            "127.0.0.1",
            isPersistent: false);

        token.IsPersistent.Should().BeFalse();
    }

    [Fact]
    public async Task LastActiveSystemAdministrator_ShouldNotLoseTheRole()
    {
        await using var context = CreateContext();
        var user = User.Create(Guid.NewGuid(), "last-admin@example.test", "Last", "Admin");
        var role = Role.Create("SystemAdmin", "System administrator", isSystemRole: true);
        user.AddRole(new UserRole(user.Id, role.Id));
        context.AddRange(user, role);
        await context.SaveChangesAsync();

        var result = await CreateService(context).RemoveRoleAsync(
            user.Id, role.Name, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.LastSystemAdmin");
        user.Roles.Should().ContainSingle();
    }

    [Fact]
    public async Task LastActiveSystemAdministrator_ShouldNotBeDeactivated()
    {
        await using var context = CreateContext();
        var user = User.Create(Guid.NewGuid(), "last-admin@example.test", "Last", "Admin");
        var role = Role.Create("SystemAdmin", "System administrator", isSystemRole: true);
        user.AddRole(new UserRole(user.Id, role.Id));
        context.AddRange(user, role);
        await context.SaveChangesAsync();

        var result = await CreateService(context).DeactivateUserAsync(
            user.Id, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.LastSystemAdmin");
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task LastActiveSystemAdministrator_ShouldNotBeDeleted()
    {
        await using var context = CreateContext();
        var user = User.Create(Guid.NewGuid(), "last-admin@example.test", "Last", "Admin");
        var role = Role.Create("SystemAdmin", "System administrator", isSystemRole: true);
        user.AddRole(new UserRole(user.Id, role.Id));
        context.AddRange(user, role);
        await context.SaveChangesAsync();

        var result = await CreateService(context).DeleteUserAsync(
            user.Id, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.LastSystemAdmin");
        (await context.Users.AnyAsync(candidate => candidate.Id == user.Id)).Should().BeTrue();
    }

    private static RefreshToken AddRefreshToken(User user)
    {
        var refreshToken = RefreshToken.Create(
            user.Id,
            $"refresh-{Guid.NewGuid():N}",
            DateTime.UtcNow.AddDays(7),
            "127.0.0.1");
        user.AddRefreshToken(refreshToken);
        return refreshToken;
    }

    private static Task<RefreshToken> ReadTokenAsync(IdentityDbContext context, Guid tokenId) =>
        context.RefreshTokens.AsNoTracking().SingleAsync(token => token.Id == tokenId);

    private static IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }

    private static LocalIdentityService CreateService(IdentityDbContext context)
    {
        var userRepository = new UserRepository(context);
        return new LocalIdentityService(
            userRepository,
            new PasswordHasher(),
            new UnitOfWork(context),
            NullLogger<LocalIdentityService>.Instance,
            new StubTokenService(),
            new RoleRepository(context),
            context,
            new SystemAdminCurrentUser());
    }

    private sealed class StubTokenService : ITokenService
    {
        public Task<int> GetAccessTokenLifetimeMinutesAsync() => Task.FromResult(15);
        public Task<string> GenerateAccessTokenAsync(User user, DateTimeOffset? mfaVerifiedAt = null) => Task.FromResult("unused");

        public RefreshToken GenerateRefreshToken(
            Guid userId,
            string ipAddress,
            bool isPersistent = true,
            DateTimeOffset? mfaVerifiedAt = null) =>
            RefreshToken.Create(
                userId,
                "unused",
                DateTime.UtcNow.AddDays(1),
                ipAddress,
                isPersistent,
                mfaVerifiedAt);
    }

    private sealed class SystemAdminCurrentUser(Guid? userId = null) : ICurrentUserService
    {
        public Guid? UserId => userId ?? Guid.NewGuid();
        public string? Email => "admin@example.test";
        public string? FullName => "System Admin";
        public IEnumerable<string> Roles => ["SystemAdmin"];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
