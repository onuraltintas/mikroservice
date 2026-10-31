using Coaching.API.Controllers;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Management;
using Coaching.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace Identity.API.IntegrationTests;

// Roadmap O6: a private calendar link Google or Apple Calendar can subscribe to. The link carries a secret token (only
// its hash is stored); creating a new link invalidates the old one and the user can turn it off.
public sealed partial class CoachingCmsAndSubscriptionServiceTests
{
    [Fact]
    public void TheCalendarFeedAnswersHeadRequestsAnonymouslyLikeGet()
    {
        // Roadmap F1: the gateway lets HEAD through for calendar apps; the service must answer it, not fall back to 401.
        var feed = typeof(Coaching.API.Controllers.CalendarController).GetMethod("Feed")!;
        feed.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute), false)
            .Cast<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>()
            .SelectMany(attribute => attribute.HttpMethods)
            .Should().BeEquivalentTo(["GET", "HEAD"]);
        feed.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), false).Should().ContainSingle();
    }

    [Fact]
    public async Task ACalendarLinkServesTheUsersSessionsUntilItIsReplacedOrTurnedOff()
    {
        await using var db = CreateDbContext();
        var teacher = Guid.NewGuid(); var student = Guid.NewGuid(); var other = Guid.NewGuid();
        var session = CoachingSession.Create(teacher, "Matematik; etüt", DateTime.UtcNow.AddDays(2), SessionType.OneOnOne, 60);
        session.AddStudent(student);
        var foreign = CoachingSession.Create(Guid.NewGuid(), "Başka öğretmen", DateTime.UtcNow.AddDays(3), SessionType.OneOnOne, 60);
        foreign.AddStudent(other);
        db.AddRange(session, foreign, Coaching.Domain.Profiles.CoachingTeacherProfile.Create(teacher),
            Coaching.Domain.Profiles.CoachingStudentProfile.Create(student));
        await db.SaveChangesAsync();
        var accounts = new CoachingOwnedProfilePersistenceTests.FakeAccounts();
        accounts.Users[teacher] = new(teacher, "Can", "Öğretmen", "can@example.test", true, true, ["Teacher"], null);
        accounts.Users[student] = new(student, "Ada", "Yılmaz", "ada@example.test", true, true, ["Student"], null);
        var service = new CoachingCalendarSubscriptionService(db, new CoachingCalendarRepository(db), TimeProvider.System, accounts);

        (await service.GetAsync(teacher, "Teacher", default)).Active.Should().BeFalse();
        var link = await service.CreateAsync(teacher, "Teacher", default);
        link.Path.Should().MatchRegex("^/api/calendar/feed/[A-Za-z0-9_-]{43}\\.ics$");
        var token = link.Path["/api/calendar/feed/".Length..^".ics".Length];
        db.ChangeTracker.Clear();

        var feed = await service.FeedAsync(token, default);
        feed.Should().NotBeNull();
        feed!.Should().Contain("BEGIN:VCALENDAR").And.Contain(@"SUMMARY:Matematik\; etüt").And.Contain("X-WR-CALNAME:Eduİvme Koçluk").And.NotContain("Başka öğretmen");
        (await service.GetAsync(teacher, "Teacher", default)).LastUsedAt.Should().NotBeNull();
        db.CoachingCalendarFeeds.Single().TokenHash.Should().NotContain(token, "only a hash of the token is stored");

        var studentLink = await service.CreateAsync(student, "Student", default);
        (await service.FeedAsync(studentLink.Path["/api/calendar/feed/".Length..^".ics".Length], default)).Should().Contain("Matematik");

        var rotated = await service.CreateAsync(teacher, "Teacher", default);
        rotated.Path.Should().NotBe(link.Path);
        (await service.FeedAsync(token, default)).Should().BeNull("the old link stops working");

        await service.RevokeAsync(teacher, "Teacher", default);
        (await service.FeedAsync(rotated.Path["/api/calendar/feed/".Length..^".ics".Length], default)).Should().BeNull();
        (await service.FeedAsync("not-a-token", default)).Should().BeNull();
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(teacher, "Admin", default));
    }

    [Fact]
    public void OnlyTheFeedIsOpenWithoutSignIn()
    {
        typeof(CalendarController).GetMethod("Feed")!.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Should().NotBeEmpty();
        typeof(CalendarController).GetMethod("CreateSubscription")!.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Should().BeEmpty();
    }
}
