using FluentAssertions;
using Identity.Application.Commands.UpdateRolePermissions;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class RolePermissionIntegrityTests
{
    [Fact]
    public async Task Update_ShouldRejectUnknownPermissionKeysWithoutChangingRole()
    {
        await using var context = CreateContext();
        var role = Role.Create("CustomRole", "Custom role");
        var valid = Permission.Create("Permissions.Users.View", "View users", "Users");
        context.AddRange(role, valid, new RolePermission(role.Id, valid.Key));
        await context.SaveChangesAsync();

        var result = await new UpdateRolePermissionsCommandHandler(
            new RoleRepository(context),
            new PermissionRepository(context),
            new UnitOfWork(context))
            .Handle(new UpdateRolePermissionsCommand(role.Id, ["Permissions.Users.View", "Permissions.DoesNotExist"]), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Role.InvalidPermission");
        (await context.RolePermissions.CountAsync(permission => permission.RoleId == role.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Update_ShouldRejectDeletedPermissionKeys()
    {
        await using var context = CreateContext();
        var role = Role.Create("CustomRole", "Custom role");
        var deleted = Permission.Create("Permissions.Users.View", "View users", "Users");
        deleted.MarkAsDeleted();
        context.AddRange(role, deleted);
        await context.SaveChangesAsync();

        var result = await new UpdateRolePermissionsCommandHandler(
            new RoleRepository(context),
            new PermissionRepository(context),
            new UnitOfWork(context))
            .Handle(new UpdateRolePermissionsCommand(role.Id, [deleted.Key]), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Role.InvalidPermission");
    }

    private static IdentityDbContext CreateContext() => new(
        new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
