using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class ProductAccessDomainTests
{
    [Theory]
    [InlineData(PlatformProduct.Coaching, PlatformProduct.SpeedReading)]
    [InlineData(PlatformProduct.SpeedReading, PlatformProduct.Coaching)]
    public void GrantProductAccess_ShouldGrantOnlyRequestedProduct(
        PlatformProduct grantedProduct,
        PlatformProduct otherProduct)
    {
        var user = User.Create(Guid.NewGuid(), "student@example.test");

        user.GrantProductAccess(
            grantedProduct,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);

        Assert.True(user.HasProductAccess(grantedProduct));
        Assert.False(user.HasProductAccess(otherProduct));
        Assert.Single(user.ProductAccesses);
    }

    [Fact]
    public void GrantProductAccess_RepeatedGrant_ShouldNotDuplicateAccess()
    {
        var user = User.Create(Guid.NewGuid(), "teacher@example.test");
        var grantedAt = DateTimeOffset.UtcNow;

        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            grantedAt);
        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.Admin,
            grantedByUserId: Guid.NewGuid(),
            grantedAt.AddMinutes(1));

        Assert.Single(user.ProductAccesses);
        Assert.True(user.HasProductAccess(PlatformProduct.Coaching));
    }

    [Fact]
    public void RevokeProductAccess_ShouldLeaveOtherProductAccessActive()
    {
        var user = User.Create(Guid.NewGuid(), "teacher@example.test");
        var now = DateTimeOffset.UtcNow;
        user.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, null, now);
        user.GrantProductAccess(PlatformProduct.SpeedReading, UserProductAccessSource.Admin, null, now);

        user.RevokeProductAccess(PlatformProduct.Coaching, now.AddMinutes(1));

        Assert.False(user.HasProductAccess(PlatformProduct.Coaching));
        Assert.True(user.HasProductAccess(PlatformProduct.SpeedReading));
        Assert.Equal(2, user.ProductAccesses.Count);
    }

    [Fact]
    public async Task ProductAccess_ShouldPersistWithUserInIdentityDatabase()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var userId = Guid.NewGuid();

        await using (var context = new IdentityDbContext(options))
        {
            var user = User.Create(userId, "student@example.test");
            user.GrantProductAccess(
                PlatformProduct.SpeedReading,
                UserProductAccessSource.SelfRegistration,
                grantedByUserId: null,
                DateTimeOffset.UtcNow);
            user.AddRefreshToken(RefreshToken.Create(
                userId,
                "speed-reading-refresh-token",
                DateTime.UtcNow.AddDays(1),
                "127.0.0.1",
                product: PlatformProduct.SpeedReading));
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        await using var readContext = new IdentityDbContext(options);
        var savedUser = await readContext.Users
            .Include(candidate => candidate.ProductAccesses)
            .Include(candidate => candidate.RefreshTokens)
            .SingleAsync(candidate => candidate.Id == userId);

        Assert.True(savedUser.HasProductAccess(PlatformProduct.SpeedReading));
        Assert.False(savedUser.HasProductAccess(PlatformProduct.Coaching));
        Assert.Equal(PlatformProduct.SpeedReading, Assert.Single(savedUser.RefreshTokens).Product);
    }

    [Fact]
    public async Task RefreshTokenLookup_ShouldRestoreThePersistedProductScope()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var userId = Guid.NewGuid();
        const string token = "speed-reading-refresh-token";

        await using (var context = new IdentityDbContext(options))
        {
            var user = User.Create(userId, "reader@example.test");
            user.GrantProductAccess(
                PlatformProduct.SpeedReading,
                UserProductAccessSource.SelfRegistration,
                grantedByUserId: null,
                DateTimeOffset.UtcNow);
            user.AddRefreshToken(RefreshToken.Create(
                userId,
                token,
                DateTime.UtcNow.AddDays(1),
                "127.0.0.1",
                product: PlatformProduct.SpeedReading));
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        await using var readContext = new IdentityDbContext(options);
        var userWithToken = await new UserRepository(readContext)
            .GetByRefreshTokenAsync(token, CancellationToken.None);

        Assert.Equal(PlatformProduct.SpeedReading, Assert.Single(userWithToken!.RefreshTokens).Product);
    }

    [Fact]
    public async Task SpeedReadingDirectory_ShouldNotReturnCoachingOnlyUsers()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var speedReadingUser = CreateUserWithAccess("reader@example.test", PlatformProduct.SpeedReading);
        var coachingUser = CreateUserWithAccess("student@example.test", PlatformProduct.Coaching);

        await using var context = new IdentityDbContext(options);
        context.Users.AddRange(speedReadingUser, coachingUser);
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);
        var result = await repository.GetSpeedReadingDirectoryAsync(
            [speedReadingUser.Id, coachingUser.Id],
            CancellationToken.None);

        Assert.Collection(result, item => Assert.Equal(speedReadingUser.Id, item.UserId));
    }

    [Fact]
    public async Task AdminUserDirectory_ShouldExposeOnlyPersistedPlatformMemberships()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var speedReadingUser = CreateUserWithAccess("reader@example.test", PlatformProduct.SpeedReading);
        var coachingUser = CreateUserWithAccess("student@example.test", PlatformProduct.Coaching);

        await using var context = new IdentityDbContext(options);
        context.Users.AddRange(speedReadingUser, coachingUser);
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);
        var result = await repository.GetAllAsync(
            page: 1,
            pageSize: 10,
            searchTerm: null,
            role: null,
            isActive: null,
            institutionId: null,
            CancellationToken.None,
            PlatformProduct.SpeedReading);

        Assert.Single(result.Items);
        var speedReadingAccess = Assert.Single(
            Assert.Single(result.Items.Where(user => user.UserId == speedReadingUser.Id)).ProductAccesses);
        Assert.Equal("speed-reading", speedReadingAccess.Product);
        Assert.True(speedReadingAccess.IsActive);

        var allUsers = await repository.GetAllAsync(
            page: 1,
            pageSize: 10,
            searchTerm: null,
            role: null,
            isActive: null,
            institutionId: null,
            CancellationToken.None);
        Assert.Equal(2, allUsers.Items.Count);
        var coachingAccess = Assert.Single(
            Assert.Single(allUsers.Items.Where(user => user.UserId == coachingUser.Id)).ProductAccesses);
        Assert.Equal("coaching", coachingAccess.Product);

        var speedReadingSummary = await repository.GetSummaryAsync(
            institutionId: null,
            CancellationToken.None,
            PlatformProduct.SpeedReading);
        Assert.Equal(1, speedReadingSummary.TotalUsers);
    }

    [Fact]
    public async Task UserDirectory_ShouldReturnRoleAssignmentsWithTheirProductScope()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var user = User.Create(Guid.NewGuid(), "cross-platform@example.test");
        user.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, null, DateTimeOffset.UtcNow);
        user.GrantProductAccess(PlatformProduct.SpeedReading, UserProductAccessSource.Admin, null, DateTimeOffset.UtcNow);
        var teacherRole = Role.Create("Teacher", "Teacher");
        var studentRole = Role.Create("Student", "Student");
        user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, teacherRole.Id, PlatformProduct.Coaching));
        user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, studentRole.Id, PlatformProduct.SpeedReading));

        await using var context = new IdentityDbContext(options);
        context.Users.Add(user);
        context.Roles.AddRange(teacherRole, studentRole);
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);
        var speedReadingUsers = await repository.GetAllAsync(
            1, 10, null, null, null, null, CancellationToken.None, PlatformProduct.SpeedReading);
        var allUsers = await repository.GetAllAsync(1, 10, null, null, null, null, CancellationToken.None);

        Assert.Collection(
            Assert.Single(speedReadingUsers.Items).ProductRoles,
            role =>
            {
                Assert.Equal("Student", role.Role);
                Assert.Equal("speed-reading", role.Product);
            });
        Assert.Equal(
            ["coaching", "speed-reading"],
            Assert.Single(allUsers.Items).ProductRoles.Select(role => role.Product).OrderBy(product => product));
    }

    [Fact]
    public async Task SpeedReadingAudience_ShouldIncludeOnlyActiveSpeedReadingMembers()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var speedReadingUser = CreateUserWithAccess("reader@example.test", PlatformProduct.SpeedReading);
        var coachingUser = CreateUserWithAccess("student@example.test", PlatformProduct.Coaching);
        var inactiveReader = CreateUserWithAccess("inactive-reader@example.test", PlatformProduct.SpeedReading);
        inactiveReader.Deactivate();

        await using var context = new IdentityDbContext(options);
        context.Users.AddRange(speedReadingUser, coachingUser, inactiveReader);
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);
        var result = await repository.GetSpeedReadingAudienceUserIdsAsync(null, CancellationToken.None);

        Assert.Equal([speedReadingUser.Id], result);
    }

    private static User CreateUserWithAccess(string email, PlatformProduct product)
    {
        var user = User.Create(Guid.NewGuid(), email);
        user.GrantProductAccess(product, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        return user;
    }
}
