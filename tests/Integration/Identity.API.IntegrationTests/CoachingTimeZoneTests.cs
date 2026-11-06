using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// E3 #34 (user decision 06.11.2026): nobody chooses a time zone any more. Every plan, reminder, session, calendar feed and
// notification uses Türkiye time (Europe/Istanbul). The time-zone setting is gone; a zone a client still sends is ignored, and
// the stored study hours, weekly draft and plan snapshot accept only Europe/Istanbul.
public sealed class CoachingTimeZoneTests
{
    [Fact]
    public void TheProductZoneIsIstanbul() => Assert.Equal("Europe/Istanbul", CoachingTime.ZoneId);

    [Fact]
    public void StoredStudyTimesAcceptOnlyIstanbul()
    {
        Assert.Equal(CoachingTime.ZoneId, StudyAvailability.Create(Guid.NewGuid(), CoachingTime.ZoneId).TimeZoneId);
        foreach (var zone in new[] { "Europe/Berlin", "UTC", "America/New_York", "Not/AZone" })
        {
            Assert.Throws<ArgumentException>(() => StudyAvailability.Create(Guid.NewGuid(), zone));
            var hours = StudyAvailability.Create(Guid.NewGuid(), CoachingTime.ZoneId);
            Assert.Throws<ArgumentException>(() => hours.ReplacePreferences(zone, []));
            var draft = WeeklyStudyDraft.Create(Guid.NewGuid());
            Assert.Throws<ArgumentException>(() => draft.Replace(zone, []));
            var plan = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Plan");
            Assert.Throws<ArgumentException>(() => plan.SetWeeklySource(new(0, null, new(2026, 11, 9), new(2026, 11, 15), false, "f", null, null, zone)));
        }
    }

    [Fact]
    public void TheTimeZoneSettingIsGone()
    {
        var routes = typeof(CoachingProfilesController).Assembly.GetTypes()
            .SelectMany(x => x.GetMethods()).SelectMany(x => x.GetCustomAttributes(typeof(HttpMethodAttribute), true).Cast<HttpMethodAttribute>())
            .Select(x => x.Template ?? "").ToArray();
        Assert.DoesNotContain(routes, x => x.Contains("time-zone", StringComparison.OrdinalIgnoreCase));
        Assert.Null(typeof(CoachingProfilesController).GetMethod("UpdateTimeZone"));
        Assert.Null(typeof(CoachingProfilesController).GetMethod("GetTimeZone"));
    }

    [Fact]
    public async Task NewStudyHoursUseIstanbulWhateverTheClientSends()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var id = f.AddAccount("Student"); f.Actor.UserId = id; f.Actor.Roles = ["Student"];
        var service = new CoachingStudyAvailabilityService(f.Db, new CoachingAccessPolicy(f.Actor));
        var created = await service.ReplaceAsync(new StudyAvailabilityUpdate(null, "America/New_York", [new(DayOfWeek.Monday, 480, 540)]));
        Assert.Equal(CoachingTime.ZoneId, created.TimeZoneId);
        // A missing zone (the new client sends none) is fine too.
        var changed = await service.ReplaceAsync(new StudyAvailabilityUpdate(created.Version, null, [new(DayOfWeek.Tuesday, 600, 660)]));
        Assert.Equal((CoachingTime.ZoneId, 600), (changed.TimeZoneId, Assert.Single(changed.Windows).StartMinute));
        Assert.Equal(CoachingTime.ZoneId, (await f.Db.StudyAvailability.AsNoTracking().SingleAsync()).TimeZoneId);
    }
}
