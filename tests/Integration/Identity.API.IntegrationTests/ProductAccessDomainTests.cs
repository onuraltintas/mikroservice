using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using UserRoleAssignment = Identity.Domain.Entities.UserRole;

namespace Identity.API.IntegrationTests;

public sealed class ProductAccessDomainTests
{
    [Theory]
    [InlineData(PlatformProduct.Coaching)]
    [InlineData(PlatformProduct.SpeedReading)]
    public void Grant_ShouldCreateAnActiveProductScopedAccess(
        PlatformProduct product)
    {
        var userId = Guid.NewGuid();

        var access = UserProductAccess.Grant(
            userId,
            product,
            ProductAccessSource.Registration);

        access.UserId.Should().Be(userId);
        access.Product.Should().Be(product);
        access.Source.Should().Be(ProductAccessSource.Registration);
        access.Status.Should().Be(ProductAccessStatus.Active);
        access.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void Revoke_ShouldDisableOnlyThatProductAccess()
    {
        var coachingAccess = UserProductAccess.Grant(
            Guid.NewGuid(),
            PlatformProduct.Coaching,
            ProductAccessSource.Registration);
        var speedReadingAccess = UserProductAccess.Grant(
            coachingAccess.UserId,
            PlatformProduct.SpeedReading,
            ProductAccessSource.Admin);

        coachingAccess.Revoke();

        coachingAccess.Status.Should().Be(ProductAccessStatus.Revoked);
        coachingAccess.RevokedAt.Should().NotBeNull();
        speedReadingAccess.Status.Should().Be(ProductAccessStatus.Active);
    }

    [Fact]
    public void UserRole_ShouldCarryItsProductScope()
    {
        var assignment = new UserRoleAssignment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PlatformProduct.Coaching);

        assignment.Product.Should().Be(PlatformProduct.Coaching);
    }

    [Fact]
    public void UserRole_ShouldAllowAnExplicitGlobalScopeForSystemRoles()
    {
        var assignment = new UserRoleAssignment(Guid.NewGuid(), Guid.NewGuid());

        assignment.Product.Should().BeNull();
    }
}
