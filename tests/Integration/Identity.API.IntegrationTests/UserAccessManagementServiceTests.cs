using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class UserAccessManagementServiceTests
{
    [Fact]
    public async Task GetActiveSessions_ShouldReturnOnlySafeActiveSessionMetadata()
    {
        await using var context = CreateContext();
        var user = User.Create(Guid.NewGuid(), "user@example.com");
        var active = RefreshToken.Create(user.Id, "secret-active", DateTime.UtcNow.AddHours(1), "127.0.0.1");
        var expired = RefreshToken.Create(user.Id, "secret-expired", DateTime.UtcNow.AddMinutes(-1), "127.0.0.2");
        var revoked = RefreshToken.Create(user.Id, "secret-revoked", DateTime.UtcNow.AddHours(1), "127.0.0.3");
        revoked.Revoke("system", "test");
        context.Add(user);
        context.AddRange(active, expired, revoked);
        await context.SaveChangesAsync();

        var result = await new UserAccessManagementService(context)
            .GetActiveSessionsAsync(user.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Id.Should().Be(active.Id);
        result.Value[0].CreatedByIp.Should().Be("127.0.0.1");
        result.Value[0].GetType().GetProperty("Token").Should().BeNull();
    }

    [Fact]
    public async Task RevokeSession_ShouldRequireSessionToBelongToUser()
    {
        await using var context = CreateContext();
        var owner = User.Create(Guid.NewGuid(), "owner@example.com");
        var other = User.Create(Guid.NewGuid(), "other@example.com");
        var session = RefreshToken.Create(other.Id, "secret", DateTime.UtcNow.AddHours(1), null);
        context.AddRange(owner, other, session);
        await context.SaveChangesAsync();

        var result = await new UserAccessManagementService(context)
            .RevokeSessionAsync(owner.Id, session.Id, "admin-ip", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        session.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ResetMfa_ShouldClearCredentialsAndRevokeEveryActiveSession()
    {
        await using var context = CreateContext();
        var user = User.Create(Guid.NewGuid(), "admin@example.com");
        user.EnableMfa("protected", ["hash"], DateTimeOffset.UtcNow);
        var first = RefreshToken.Create(user.Id, "first", DateTime.UtcNow.AddHours(1), null);
        var second = RefreshToken.Create(user.Id, "second", DateTime.UtcNow.AddHours(1), null);
        context.AddRange(user, first, second);
        await context.SaveChangesAsync();

        var result = await new UserAccessManagementService(context)
            .ResetMfaAsync(user.Id, "admin-ip", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.MfaEnabled.Should().BeFalse();
        first.IsRevoked.Should().BeTrue();
        second.IsRevoked.Should().BeTrue();
        first.ReasonRevoked.Should().Be("MFA reset by administrator");
    }

    private static IdentityDbContext CreateContext() => new(
        new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
