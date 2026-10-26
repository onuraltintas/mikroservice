using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class ProductSessionContextTests
{
    [Fact]
    public void Resolve_ShouldInferTheOnlyActiveProduct()
    {
        var user = User.Create(Guid.NewGuid(), "student@example.test");
        user.AddProductAccess(UserProductAccess.Grant(
            user.Id,
            PlatformProduct.Coaching,
            ProductAccessSource.Registration));

        var result = ProductSessionContext.Resolve(user, requestedProduct: null, isSystemAdministrator: false);

        Assert.True(result.IsSuccess);
        Assert.Equal(PlatformProduct.Coaching, result.Value);
    }

    [Fact]
    public void Resolve_ShouldRequireExplicitContextWhenUserHasMultipleProducts()
    {
        var user = User.Create(Guid.NewGuid(), "student@example.test");
        user.AddProductAccess(UserProductAccess.Grant(user.Id, PlatformProduct.Coaching, ProductAccessSource.Registration));
        user.AddProductAccess(UserProductAccess.Grant(user.Id, PlatformProduct.SpeedReading, ProductAccessSource.Admin));

        var result = ProductSessionContext.Resolve(user, requestedProduct: null, isSystemAdministrator: false);

        Assert.True(result.IsFailure);
        Assert.Equal("Auth.ProductContextRequired", result.Error.Code);
    }

    [Fact]
    public void Resolve_ShouldRejectAProductWithoutActiveEntitlement()
    {
        var user = User.Create(Guid.NewGuid(), "student@example.test");
        user.AddProductAccess(UserProductAccess.Grant(
            user.Id,
            PlatformProduct.Coaching,
            ProductAccessSource.Registration));

        var result = ProductSessionContext.Resolve(user, PlatformProduct.SpeedReading, isSystemAdministrator: false);

        Assert.True(result.IsFailure);
        Assert.Equal("Auth.ProductAccessDenied", result.Error.Code);
    }

    [Fact]
    public void Resolve_ShouldAllowUnscopedSystemAdministratorSession()
    {
        var user = User.Create(Guid.NewGuid(), "admin@example.test");

        var result = ProductSessionContext.Resolve(user, requestedProduct: null, isSystemAdministrator: true);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }
}
