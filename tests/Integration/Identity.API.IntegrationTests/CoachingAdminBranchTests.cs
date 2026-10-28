using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Roadmap O13: an admin of several institutions (branches) may choose which one a request is for.
public sealed class CoachingAdminBranchTests
{
    [Fact]
    public async Task AdminOfSeveralBranchesGetsTheChosenOneOnlyWhenItIsTheirs()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var role = Role.Create("InstitutionAdmin", "Kurum yöneticisi");
        var admin = User.Create(Guid.NewGuid(), "yonetici@example.test", "Ayşe", "Yılmaz");
        admin.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        admin.AddRole(new Identity.Domain.Entities.UserRole(admin.Id, role.Id, PlatformProduct.Coaching));
        var first = Institution.Create("Merkez", InstitutionType.PrivateCourse); var second = Institution.Create("Kadıköy", InstitutionType.PrivateCourse);
        var foreign = Institution.Create("Başka kurum", InstitutionType.PrivateCourse);
        db.AddRange(role, admin, first, second, foreign,
            InstitutionAdmin.Create(admin.Id, first.Id, InstitutionAdminRole.Admin, PlatformProduct.Coaching));
        await db.SaveChangesAsync();
        db.Add(InstitutionAdmin.Create(admin.Id, second.Id, InstitutionAdminRole.Admin, PlatformProduct.Coaching)); await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var repository = new InstitutionRepository(db);

        var fallback = await repository.AuthorizeCoachingAdminAsync(admin.Id, null, default);
        Assert.Equal(first.Id, fallback!.InstitutionId);
        Assert.Equal(new[] { "Merkez", "Kadıköy" }, fallback.Institutions.Select(x => x.Name));
        Assert.Equal(second.Id, (await repository.AuthorizeCoachingAdminAsync(admin.Id, second.Id, default))!.InstitutionId);
        Assert.Equal(first.Id, (await repository.AuthorizeCoachingAdminAsync(admin.Id, foreign.Id, default))!.InstitutionId);
    }
}
