using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Identity.API.IntegrationTests;

public sealed class SpeedReadingInstitutionMemberEligibilityTests
{
    [Fact]
    public async Task Eligibility_requires_active_speed_reading_access_and_the_matching_product_role()
    {
        await using var context = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var user = User.Create(Guid.NewGuid(), "reader@eligibility.test", "Speed", "Reader");
        user.GrantProductAccess(
            PlatformProduct.SpeedReading,
            UserProductAccessSource.Admin,
            grantedByUserId: null,
            DateTimeOffset.UtcNow);
        AddRole(user, "Student", PlatformProduct.SpeedReading);
        AddRole(user, "Teacher", PlatformProduct.Coaching);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);

        Assert.True(await repository.IsSpeedReadingMembershipEligibleAsync(user.Id, "Student", CancellationToken.None));
        Assert.False(await repository.IsSpeedReadingMembershipEligibleAsync(user.Id, "Teacher", CancellationToken.None));
        Assert.False(await repository.IsSpeedReadingMembershipEligibleAsync(user.Id, "InstitutionAdmin", CancellationToken.None));
    }

    [Fact]
    public async Task Institution_manager_scope_requires_an_active_speed_reading_assignment()
    {
        await using var context = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var institution = Institution.Create("Speed Reading Institution", InstitutionType.School);
        var speedReadingAdmin = CreateInstitutionManager(
            "speed-reading-admin@test.test",
            institution.Id,
            PlatformProduct.SpeedReading);
        var coachingAdmin = CreateInstitutionManager(
            "coaching-admin@test.test",
            institution.Id,
            PlatformProduct.Coaching);
        var inactiveAdmin = CreateInstitutionManager(
            "inactive-admin@test.test",
            institution.Id,
            PlatformProduct.SpeedReading);
        var inactiveAssignment = InstitutionAdmin.Create(
            inactiveAdmin.Id,
            institution.Id,
            InstitutionAdminRole.Owner,
            PlatformProduct.SpeedReading);
        inactiveAssignment.Deactivate();

        context.Institutions.Add(institution);
        context.Users.AddRange(speedReadingAdmin, coachingAdmin, inactiveAdmin);
        context.InstitutionAdmins.AddRange(
            InstitutionAdmin.Create(speedReadingAdmin.Id, institution.Id, InstitutionAdminRole.Owner, PlatformProduct.SpeedReading),
            InstitutionAdmin.Create(coachingAdmin.Id, institution.Id, InstitutionAdminRole.Owner, PlatformProduct.Coaching),
            inactiveAssignment);
        await context.SaveChangesAsync();

        var repository = new InstitutionRepository(context);

        Assert.True(await repository.CanManageSpeedReadingInstitutionAsync(
            speedReadingAdmin.Id,
            institution.Id,
            CancellationToken.None));
        Assert.False(await repository.CanManageSpeedReadingInstitutionAsync(
            coachingAdmin.Id,
            institution.Id,
            CancellationToken.None));
        Assert.False(await repository.CanManageSpeedReadingInstitutionAsync(
            inactiveAdmin.Id,
            institution.Id,
            CancellationToken.None));
    }

    private static void AddRole(User user, string roleName, PlatformProduct product)
    {
        var role = Role.Create(roleName, roleName);
        var userRole = new Identity.Domain.Entities.UserRole(user.Id, role.Id, product);
        typeof(Identity.Domain.Entities.UserRole).GetProperty(nameof(Identity.Domain.Entities.UserRole.Role))!
            .SetValue(userRole, role);
        user.AddRole(userRole);
    }

    private static User CreateInstitutionManager(string email, Guid institutionId, PlatformProduct product)
    {
        var user = User.Create(Guid.NewGuid(), email, "Institution", "Manager");
        user.GrantProductAccess(product, UserProductAccessSource.Admin, grantedByUserId: null, DateTimeOffset.UtcNow);
        AddRole(user, "InstitutionOwner", product);
        return user;
    }
}
