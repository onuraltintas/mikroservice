using Identity.Domain.Enums;

namespace CourseManagement.Application.UnitTests;

public sealed class ProductIsolationTests
{
    [Fact]
    public void CourseProductUsesNewValueWithoutRenumberingExistingProducts()
    {
        Assert.Equal(1, (int)PlatformProduct.Coaching);
        Assert.Equal(2, (int)PlatformProduct.SpeedReading);
        Assert.True(PlatformProductExtensions.TryParseRouteValue("course-management", out var product));
        Assert.Equal(3, (int)product);
        Assert.Equal("course-management", product.ToRouteValue());
    }

    [Theory]
    [InlineData(PlatformProduct.Coaching)]
    [InlineData(PlatformProduct.SpeedReading)]
    public void ExistingProductCannotIssueCoursePermissions(PlatformProduct product)
    {
        Assert.False(PlatformPermissionScope.IsAllowed("Permissions.CourseManagement.View", product));
    }

    [Theory]
    [InlineData("Permissions.Coaching.Manage")]
    [InlineData("Permissions.SpeedReading.View")]
    public void CourseProductCannotIssueOtherProductPermissions(string permission)
    {
        Assert.False(PlatformPermissionScope.IsAllowed(permission, (PlatformProduct)3));
    }

    [Theory]
    [InlineData(PlatformProduct.Coaching, "Permissions.Coaching.Manage", true)]
    [InlineData(PlatformProduct.Coaching, "Permissions.SpeedReading.View", false)]
    [InlineData(PlatformProduct.SpeedReading, "Permissions.SpeedReading.View", true)]
    [InlineData(PlatformProduct.SpeedReading, "Permissions.Coaching.Manage", false)]
    [InlineData(PlatformProduct.Coaching, "Permissions.Users.View", true)]
    [InlineData(PlatformProduct.SpeedReading, "Permissions.Users.View", true)]
    public void ExistingPermissionDecisionsArePreserved(PlatformProduct product, string permission, bool allowed)
    {
        Assert.Equal(allowed, PlatformPermissionScope.IsAllowed(permission, product));
    }

    [Fact]
    public void GlobalAdministratorPermissionScopeIsPreserved()
    {
        Assert.True(PlatformPermissionScope.IsAllowed("Permissions.CourseManagement.View", null));
    }
}
