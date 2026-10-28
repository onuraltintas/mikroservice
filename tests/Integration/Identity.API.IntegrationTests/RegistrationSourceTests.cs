using FluentAssertions;
using Identity.API.Controllers;
using Identity.Application.Registration;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Roadmap O11: where a registration came from (utm_source / utm_medium / utm_campaign of the landing page) is kept with
// the account so the admin can see which campaigns bring sign-ups, also for visitors who refused analytics cookies.
public sealed class RegistrationSourceTests
{
    [Fact]
    public void CampaignTagsAreNormalizedAndEmptyTagsAreDropped()
    {
        RegistrationUtm.Normalize(new(" Instagram ", "Paid Social", "Ekim_Kampanyası"))
            .Should().Be(new RegistrationUtm("instagram", "paid-social", "ekim_kampanyasi"));
        RegistrationUtm.Normalize(new("<script>alert(1)</script>", null, "  ")).Should().Be(new RegistrationUtm("scriptalert1script", null, null));
        RegistrationUtm.Normalize(new(new string('a', 300), null, null))!.UtmSource.Should().HaveLength(100);
        RegistrationUtm.Normalize(new(" ", "", null)).Should().BeNull();
        RegistrationUtm.Normalize(null).Should().BeNull();
    }

    [Fact]
    public async Task TaggedRegistrationsAreStoredAndReportedNextToTheUntaggedOnes()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var now = DateTimeOffset.UtcNow;
        var users = Enumerable.Range(0, 4).Select(i => User.Create(Guid.NewGuid(), $"u{i}@example.test")).ToArray();
        foreach (var user in users) user.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.SelfRegistration, null, now.AddDays(-1));
        users[3].GrantProductAccess(PlatformProduct.SpeedReading, UserProductAccessSource.SelfRegistration, null, now.AddDays(-1));
        db.Users.AddRange(users);
        await db.SaveChangesAsync();

        var tracker = new RegistrationLegalConsentService(db, null!);
        tracker.TrackSource(users[0].Id, PlatformProduct.Coaching, new("google", "cpc", "ekim"), "password");
        tracker.TrackSource(users[1].Id, PlatformProduct.Coaching, new("google", "cpc", "ekim"), "google");
        tracker.TrackSource(users[2].Id, PlatformProduct.Coaching, null, "password");
        await db.SaveChangesAsync();
        (await db.RegistrationSources.CountAsync()).Should().Be(2, "a registration without tags stores nothing");

        var report = await new RegistrationSourceReport(db).GetAsync(PlatformProduct.Coaching, now.AddDays(-30), now.AddMinutes(1), default);
        report.TotalRegistrations.Should().Be(4);
        report.Untagged.Should().Be(2);
        report.Rows.Should().ContainSingle().Which.Should().Be(new RegistrationSourceRow("google", "cpc", "ekim", 2));

        (await new RegistrationSourceReport(db).GetAsync(PlatformProduct.Coaching, now.AddDays(-30), now.AddDays(-2), default)).TotalRegistrations.Should().Be(0);
    }

    [Fact]
    public void TheReportIsForUserAdministrators()
    {
        typeof(RegistrationSourcesController).GetMethod("Get")!.GetCustomAttributes(typeof(HasPermissionAttribute), true).Should().NotBeEmpty();
    }
}
