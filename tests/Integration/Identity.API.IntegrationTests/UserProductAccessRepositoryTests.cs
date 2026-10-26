using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class UserProductAccessRepositoryTests
{
    [Fact]
    public async Task EnsureProductAccess_ShouldBeIdempotentAndKeepProductsIndependent()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new IdentityDbContext(options);
        var user = User.Create(Guid.NewGuid(), "products@example.test");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);
        await repository.EnsureProductAccessAsync(
            user.Id,
            PlatformProduct.Coaching,
            ProductAccessSource.Registration,
            null,
            CancellationToken.None);
        await repository.EnsureProductAccessAsync(
            user.Id,
            PlatformProduct.Coaching,
            ProductAccessSource.Registration,
            null,
            CancellationToken.None);
        await repository.EnsureProductAccessAsync(
            user.Id,
            PlatformProduct.SpeedReading,
            ProductAccessSource.Registration,
            null,
            CancellationToken.None);
        await context.SaveChangesAsync();

        var access = await context.UserProductAccesses
            .Where(item => item.UserId == user.Id)
            .OrderBy(item => item.Product)
            .ToListAsync();

        access.Should().HaveCount(2);
        access.Select(item => item.Product).Should().Equal(
            PlatformProduct.Coaching,
            PlatformProduct.SpeedReading);
        access.Should().OnlyContain(item => item.Status == ProductAccessStatus.Active);
    }
}
