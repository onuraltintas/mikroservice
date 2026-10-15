using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class AdminProductAccessManagementTests
{
    [Theory]
    [InlineData(PlatformProduct.Coaching, "Student")]
    [InlineData(PlatformProduct.SpeedReading, "Student")]
    [InlineData(PlatformProduct.Coaching, "Teacher")]
    [InlineData(PlatformProduct.SpeedReading, "Teacher")]
    [InlineData(PlatformProduct.Coaching, "InstitutionAdmin")]
    [InlineData(PlatformProduct.SpeedReading, "InstitutionAdmin")]
    [InlineData(PlatformProduct.Coaching, "InstitutionOwner")]
    [InlineData(PlatformProduct.SpeedReading, "InstitutionOwner")]
    public async Task Revoke_and_reactivate_only_changes_selected_access_and_sessions(PlatformProduct product, string roleName)
    {
        await using var db = CreateContext();
        var user = User.Create(Guid.NewGuid(), "dual@example.test");
        var role = Role.Create(roleName, roleName);
        var actor = Guid.NewGuid();
        foreach (var scope in new[] { PlatformProduct.Coaching, PlatformProduct.SpeedReading })
        {
            user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id, scope));
            user.GrantProductAccess(scope, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        }
        var selected = RefreshToken.Create(user.Id, "selected", DateTime.UtcNow.AddHours(1), null, product: product);
        var otherProduct = product == PlatformProduct.Coaching ? PlatformProduct.SpeedReading : PlatformProduct.Coaching;
        var other = RefreshToken.Create(user.Id, "other", DateTime.UtcNow.AddHours(1), null, product: otherProduct);
        db.AddRange(user, role, selected, other);
        await db.SaveChangesAsync();
        var roleIds = user.Roles.Select(r => r.Id).ToArray();
        var accessId = user.ProductAccesses.Single(a => a.Product == product).Id;
        var service = new UserAccessManagementService(db);

        Assert.True((await service.SetProductAccessAsync(user.Id, product, false, actor, "admin-ip", CancellationToken.None)).IsSuccess);
        Assert.False(user.HasProductAccess(product));
        Assert.True(user.HasProductAccess(otherProduct));
        Assert.True(selected.IsRevoked);
        Assert.True(other.IsActive);
        Assert.Equal(roleIds, user.Roles.Select(r => r.Id));

        Assert.True((await service.SetProductAccessAsync(user.Id, product, true, actor, "admin-ip", CancellationToken.None)).IsSuccess);
        Assert.True((await service.SetProductAccessAsync(user.Id, product, true, actor, "admin-ip", CancellationToken.None)).IsSuccess);
        Assert.True(user.HasProductAccess(product));
        Assert.Equal(2, user.ProductAccesses.Count);
        var restored = user.ProductAccesses.Single(a => a.Product == product);
        Assert.Equal(accessId, restored.Id);
        Assert.Equal(actor, restored.GrantedByUserId);
        Assert.Equal(UserProductAccessSource.Admin, restored.Source);
        Assert.Null(restored.RevokedAt);
        Assert.True(selected.IsRevoked);
        Assert.True(other.IsActive);
        Assert.Equal(roleIds, user.Roles.Select(r => r.Id));
    }

    [Theory]
    [InlineData(PlatformProduct.Coaching)]
    [InlineData(PlatformProduct.SpeedReading)]
    public async Task Grant_without_role_does_not_create_or_copy_roles_or_profiles(PlatformProduct product)
    {
        await using var db = CreateContext();
        var user = User.Create(Guid.NewGuid(), "new-access@example.test");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        Assert.True((await new UserAccessManagementService(db).SetProductAccessAsync(user.Id, product, true, Guid.NewGuid(), null, CancellationToken.None)).IsSuccess);
        db.ChangeTracker.Clear();
        var saved = await db.Users.Include(u => u.ProductAccesses).Include(u => u.Roles).SingleAsync();
        Assert.True(saved.HasProductAccess(product));
        Assert.Empty(saved.Roles);
        Assert.False(await db.StudentProfiles.AnyAsync());
        Assert.False(await db.TeacherProfiles.AnyAsync());
    }

    [Theory]
    [InlineData((PlatformProduct)0)]
    [InlineData(PlatformProduct.CourseManagement)]
    [InlineData((PlatformProduct)99)]
    public async Task Unsupported_product_is_rejected(PlatformProduct product)
    {
        await using var db = CreateContext();
        var result = await new UserAccessManagementService(db).SetProductAccessAsync(Guid.NewGuid(), product, true, Guid.NewGuid(), null, CancellationToken.None);
        Assert.Equal("Auth.InvalidProduct", result.Error.Code);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Repeated_revocation_records_inactive_access_without_creating_roles()
    {
        await using var db = CreateContext();
        var user = User.Create(Guid.NewGuid(), "absent@example.test");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new UserAccessManagementService(db);
        var actor = Guid.NewGuid();
        for (var attempt = 0; attempt < 2; attempt++)
            Assert.True((await service.SetProductAccessAsync(user.Id, PlatformProduct.Coaching, false, actor, null, CancellationToken.None)).IsSuccess);
        Assert.False(Assert.Single(user.ProductAccesses).IsActive);
        Assert.Empty(user.Roles);
    }

    [Fact]
    public async Task Missing_user_is_not_created()
    {
        await using var db = CreateContext();
        var result = await new UserAccessManagementService(db).SetProductAccessAsync(Guid.NewGuid(), PlatformProduct.Coaching, true, Guid.NewGuid(), null, CancellationToken.None);
        Assert.Equal("User.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task Global_administrator_cannot_be_misleadingly_restricted_by_product_access()
    {
        await using var db = CreateContext();
        var role = Role.Create("SystemAdmin", "Admin");
        var user = User.Create(Guid.NewGuid(), "admin@example.test");
        user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id, null));
        db.AddRange(user, role);
        await db.SaveChangesAsync();
        var result = await new UserAccessManagementService(db).SetProductAccessAsync(user.Id, PlatformProduct.Coaching, false, Guid.NewGuid(), null, CancellationToken.None);
        Assert.Equal("User.GlobalAdministrator", result.Error.Code);
        Assert.Empty(user.ProductAccesses);
    }

    private static IdentityDbContext CreateContext() => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
