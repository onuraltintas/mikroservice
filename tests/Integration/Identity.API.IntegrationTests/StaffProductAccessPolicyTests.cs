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
