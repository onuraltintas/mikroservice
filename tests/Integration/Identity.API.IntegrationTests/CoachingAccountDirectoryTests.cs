using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAccountDirectoryTests
{
    [Fact]
    public async Task CoreDirectoryUsesProductRolesWithoutLoadingEducationProfilesOrSecrets()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var studentRole = Role.Create("Student", "Student");
        var teacherRole = Role.Create("Teacher", "Teacher");
        var user = User.Create(Guid.NewGuid(), "Mixed@Example.test", "Core", "Name");
        user.SetPassword([1, 2, 3], [4, 5, 6]);
        user.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, studentRole.Id, PlatformProduct.Coaching));
        user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, teacherRole.Id, PlatformProduct.SpeedReading));
        db.AddRange(studentRole, teacherRole, user);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var reader = new CoachingAccountReader(db);
        var account = Assert.Single(await reader.GetAsync([user.Id], default));
        Assert.Equal("Core", account.FirstName);
        Assert.True(account.IsEligible("Student"));
        Assert.False(account.IsEligible("Teacher"));
        Assert.Equal(user.Id, (await reader.FindByEmailAsync("mixed@example.test", default))!.UserId);
        Assert.Empty(db.StudentProfiles); Assert.Empty(db.TeacherProfiles);
        var json = System.Text.Json.JsonSerializer.Serialize(account);
        Assert.DoesNotContain("Password", json);
        Assert.DoesNotContain("School", json);
    }
}
