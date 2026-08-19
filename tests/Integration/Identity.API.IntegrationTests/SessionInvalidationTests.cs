using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using EduPlatform.Shared.Security.Services;
using FluentAssertions;
using Identity.Application.Interfaces;
using Identity.Application.Commands.ChangePassword;
using Identity.Application.Commands.ResetPassword;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class SessionInvalidationTests
{
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
        public string GenerateAccessToken(User user) => "unused";

        public RefreshToken GenerateRefreshToken(Guid userId, string ipAddress) =>
            RefreshToken.Create(userId, "unused", DateTime.UtcNow.AddDays(1), ipAddress);
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
