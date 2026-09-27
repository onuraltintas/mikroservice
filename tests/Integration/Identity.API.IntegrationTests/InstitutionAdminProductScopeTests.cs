using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class InstitutionAdminProductScopeTests
{
    [Theory]
    [InlineData(PlatformProduct.Coaching)]
    [InlineData(PlatformProduct.SpeedReading)]
    public void Create_AssociatesInstitutionAdminWithProduct(PlatformProduct product)
    {
        var userId = Guid.NewGuid();
        var institutionId = Guid.NewGuid();

        var membership = InstitutionAdmin.Create(
            userId,
            institutionId,
            InstitutionAdminRole.Owner,
            product);

        Assert.Equal(product, membership.Product);
    }

    [Fact]
    public void Create_RejectsUndefinedProduct()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InstitutionAdmin.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            InstitutionAdminRole.Owner,
            (PlatformProduct)999));
    }

    [Fact]
    public void EfModel_StoresOptionalProductScopeForLegacyMemberships()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=identity_model_only;Username=unused;Password=unused")
            .Options;

        using var context = new IdentityDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(InstitutionAdmin))!;
        var productProperty = entityType.FindProperty(nameof(InstitutionAdmin.Product))!;

        Assert.True(productProperty.IsNullable);
        Assert.Equal(typeof(string), productProperty.GetProviderClrType());
        Assert.Contains(entityType.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(InstitutionAdmin.UserId), nameof(InstitutionAdmin.InstitutionId), nameof(InstitutionAdmin.Product)]));
        Assert.DoesNotContain(entityType.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(InstitutionAdmin.UserId), nameof(InstitutionAdmin.InstitutionId)]));
    }
}
