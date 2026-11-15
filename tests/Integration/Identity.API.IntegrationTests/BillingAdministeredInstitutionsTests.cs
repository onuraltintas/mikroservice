using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

/// <summary>What the billing service asks Identity before it lets somebody buy for an institution.</summary>
public sealed class BillingAdministeredInstitutionsTests
{
    [Fact]
    public async Task Only_active_admins_of_active_institutions_are_named_once_per_institution()
    {
        await using var context = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var school = Institution.Create("B Okulu", InstitutionType.School);
        var course = Institution.Create("A Kursu", InstitutionType.School);
        var closed = Institution.Create("Kapalı Kurum", InstitutionType.School);
        closed.Deactivate();
        var admin = User.Create(Guid.NewGuid(), "admin@billing.test", "Kurum", "Yöneticisi");
        var former = User.Create(Guid.NewGuid(), "former@billing.test", "Eski", "Yönetici");
        var teacher = User.Create(Guid.NewGuid(), "teacher@billing.test", "Bir", "Öğretmen");
        var removed = InstitutionAdmin.Create(former.Id, school.Id, InstitutionAdminRole.Owner, PlatformProduct.Coaching);
        removed.Deactivate();
        context.Institutions.AddRange(school, course, closed);
        context.Users.AddRange(admin, former, teacher);
        context.InstitutionAdmins.AddRange(
            // The same institution in two products is one institution, and one billing account.
            InstitutionAdmin.Create(admin.Id, school.Id, InstitutionAdminRole.Owner, PlatformProduct.Coaching),
            InstitutionAdmin.Create(admin.Id, school.Id, InstitutionAdminRole.Owner, PlatformProduct.SpeedReading),
            InstitutionAdmin.Create(admin.Id, course.Id, InstitutionAdminRole.Owner, PlatformProduct.Coaching),
            InstitutionAdmin.Create(admin.Id, closed.Id, InstitutionAdminRole.Owner, PlatformProduct.Coaching),
            removed);
        await context.SaveChangesAsync();
        var repository = new InstitutionRepository(context);

        var administered = await repository.GetAdministeredInstitutionsAsync(admin.Id, CancellationToken.None);

        Assert.Equal([(course.Id, "A Kursu"), (school.Id, "B Okulu")], administered.Select(item => (item.Id, item.Name)));
        Assert.Empty(await repository.GetAdministeredInstitutionsAsync(former.Id, CancellationToken.None));
        Assert.Empty(await repository.GetAdministeredInstitutionsAsync(teacher.Id, CancellationToken.None));
    }

    [Fact]
    public void The_endpoint_is_reachable_only_with_the_internal_service_key()
    {
        var action = typeof(Identity.API.Controllers.InternalBillingController).GetMethod(nameof(Identity.API.Controllers.InternalBillingController.AdministeredInstitutions))!;
        Assert.Contains(action.GetCustomAttributes(inherit: true), attribute => attribute is Identity.API.InternalServiceKeyAttribute);
        var route = (Microsoft.AspNetCore.Mvc.RouteAttribute)typeof(Identity.API.Controllers.InternalBillingController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.RouteAttribute), inherit: true).Single();
        Assert.Equal("api/internal/billing", route.Template);
    }
}
