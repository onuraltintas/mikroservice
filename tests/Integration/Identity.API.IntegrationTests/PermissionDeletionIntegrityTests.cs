using FluentAssertions;
using Identity.Application.Commands.DeletePermission;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class PermissionDeletionIntegrityTests
{
    [Fact]
    public async Task Delete_ShouldRemovePermissionFromEveryRole()
    {
        await using var context = CreateContext();
        var permission = Permission.Create("Permissions.Users.Edit", "Edit users", "Users");
        var firstRole = Role.Create("First", "First");
        var secondRole = Role.Create("Second", "Second");
        context.AddRange(permission, firstRole, secondRole,
            new RolePermission(firstRole.Id, permission.Key),
            new RolePermission(secondRole.Id, permission.Key));
        await context.SaveChangesAsync();

        var result = await new DeletePermissionCommandHandler(
            new PermissionRepository(context),
            new RoleRepository(context),
            new UnitOfWork(context))
            .Handle(new DeletePermissionCommand(permission.Id, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.RolePermissions.AnyAsync(item => item.Permission == permission.Key)).Should().BeFalse();
        (await context.Permissions.SingleAsync(item => item.Id == permission.Id)).IsDeleted.Should().BeTrue();
    }

    private static IdentityDbContext CreateContext() => new(
        new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
