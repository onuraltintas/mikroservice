using FluentAssertions;
using Identity.Application.Authorization;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class StaffProductAccessPolicyTests
{
    [Fact]
    public void GetProductAccesses_ShouldKeepStaffRolesSeparatedByProduct()
    {
        var user = User.Create(Guid.NewGuid(), "dual-staff@example.com");
        AddRole(user, "Teacher", PlatformProduct.Coaching);
        AddRole(user, "InstitutionAdmin", PlatformProduct.SpeedReading);
        AddRole(user, "Student", PlatformProduct.Coaching);
        user.GrantProductAccess(
            PlatformProduct.Coaching,
            UserProductAccessSource.Admin,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        user.GrantProductAccess(
            PlatformProduct.SpeedReading,
            UserProductAccessSource.Admin,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);

        var accesses = StaffProductAccessPolicy.GetProductAccesses(user);

        accesses.Should().BeEquivalentTo(
        [
            new StaffPortalProductAccess(PlatformProduct.Coaching, ["Teacher"]),
            new StaffPortalProductAccess(PlatformProduct.SpeedReading, ["InstitutionAdmin"])
        ]);
    }

    [Theory]
    [InlineData("SystemAdmin")]
    [InlineData("Editor")]
    public void GetProductAccesses_ShouldExcludePrivilegedRolesFromStaffSessions(string privilegedRole)
    {
        var user = User.Create(Guid.NewGuid(), "admin-teacher@example.com");
        AddRole(user, privilegedRole, PlatformProduct.SpeedReading, isSystemRole: true);
        AddRole(user, "Teacher", PlatformProduct.SpeedReading);
        user.GrantProductAccess(
            PlatformProduct.SpeedReading,
            UserProductAccessSource.Admin,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);

        var accesses = StaffProductAccessPolicy.GetProductAccesses(user);

        accesses.Should().BeEmpty();
    }

    // User decision 23.11.2026: the staff portal no longer asks for a product; the service picks it for the address.
    [Theory]
    [InlineData("institution", null, PlatformProduct.SpeedReading)]
    [InlineData("teacher", null, PlatformProduct.Coaching)]
    [InlineData(null, null, PlatformProduct.Coaching)]
    [InlineData(null, PlatformProduct.SpeedReading, PlatformProduct.SpeedReading)]
    [InlineData("teacher", PlatformProduct.SpeedReading, PlatformProduct.Coaching)]
    public void ResolveSignInProduct_ShouldOpenTheProductWithARoleForTheAddress(
        string? workspace, PlatformProduct? preferred, PlatformProduct expected)
    {
        var user = User.Create(Guid.NewGuid(), "teacher-and-manager@example.com");
        AddRole(user, "Teacher", PlatformProduct.Coaching);
        AddRole(user, "InstitutionAdmin", PlatformProduct.SpeedReading);
        Grant(user, PlatformProduct.Coaching);
        Grant(user, PlatformProduct.SpeedReading);

        StaffProductAccessPolicy.ResolveSignInProduct(user, workspace, preferred).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, PlatformProduct.Coaching)]
    [InlineData(PlatformProduct.SpeedReading, PlatformProduct.SpeedReading)]
    [InlineData(PlatformProduct.CourseManagement, PlatformProduct.Coaching)]
    public void ResolveSignInProduct_WithTwoFittingProducts_ShouldPreferTheLastUsedOtherwiseMaxkoc(
        PlatformProduct? preferred, PlatformProduct expected)
    {
        var user = User.Create(Guid.NewGuid(), "two-products@example.com");
        AddRole(user, "Teacher", PlatformProduct.Coaching);
        AddRole(user, "Teacher", PlatformProduct.SpeedReading);
        Grant(user, PlatformProduct.Coaching);
        Grant(user, PlatformProduct.SpeedReading);

        StaffProductAccessPolicy.ResolveSignInProduct(user, "teacher", preferred).Should().Be(expected);
    }

    [Fact]
    public void ResolveSignInProduct_WithOnlySpeedReading_ShouldOpenItEvenWhenTheAddressDoesNotFit()
    {
        var user = User.Create(Guid.NewGuid(), "reading-teacher@example.com");
        AddRole(user, "Teacher", PlatformProduct.SpeedReading);
        Grant(user, PlatformProduct.SpeedReading);

        StaffProductAccessPolicy.ResolveSignInProduct(user, "institution", null).Should().Be(PlatformProduct.SpeedReading);
    }

    [Fact]
    public void ResolveSignInProduct_WithoutAStaffRole_ShouldReturnNothing()
    {
        var user = User.Create(Guid.NewGuid(), "student@example.com");
        AddRole(user, "Student", PlatformProduct.Coaching);
        AddRole(user, "CourseTeacher", PlatformProduct.CourseManagement);
        Grant(user, PlatformProduct.Coaching);
        Grant(user, PlatformProduct.CourseManagement);

        StaffProductAccessPolicy.ResolveSignInProduct(user, "teacher", null).Should().BeNull();
    }

    private static void Grant(User user, PlatformProduct product) =>
        user.GrantProductAccess(product, UserProductAccessSource.Admin, grantedByUserId: null, DateTimeOffset.UtcNow);

    private static void AddRole(
        User user,
        string roleName,
        PlatformProduct? product,
        bool isSystemRole = false)
    {
        var role = Role.Create(roleName, roleName, isSystemRole);
        var userRole = new Identity.Domain.Entities.UserRole(user.Id, role.Id, product);
        typeof(Identity.Domain.Entities.UserRole).GetProperty(nameof(Identity.Domain.Entities.UserRole.Role))!
            .SetValue(userRole, role);
        user.AddRole(userRole);
    }
}
