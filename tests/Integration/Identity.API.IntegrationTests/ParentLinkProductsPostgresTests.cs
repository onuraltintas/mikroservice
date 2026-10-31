using FluentAssertions;
using Identity.Application.ParentLinks;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using IdentityUserRole = Identity.Domain.Enums.UserRole;

namespace Identity.API.IntegrationTests;

/// <summary>
/// V1 (parent panel), on real PostgreSQL with the real migrations: the student decides per product what the parent
/// sees, other services ask Identity before a parent reads a child's data, and the weekly-summary recipients are paged
/// in SQL with the product flags the summary needs.
/// </summary>
public sealed class ParentLinkProductsPostgresTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private IdentityPostgresDatabase _database = null!;
    private Role _parentRole = null!;
    private Role _studentRole = null!;

    public async Task InitializeAsync()
    {
        _database = await IdentityPostgresDatabase.CreateAsync();
        _parentRole = Role.Create(IdentityUserRole.Parent.ToString(), "Parent", true);
        _studentRole = Role.Create(IdentityUserRole.Student.ToString(), "Student", true);
        await using var context = _database.CreateContext();
        context.AddRange(_parentRole, _studentRole);
        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task LinksListBothProductsWithTheChildsAccessAndWhatTheParentSees()
    {
        var parent = await AddUserAsync("veli@example.test", "Ayşe", _parentRole);
        var student = await AddUserAsync("ogrenci@example.test", "Ada", _studentRole, PlatformProduct.Coaching);
        var link = await LinkAsync(parent, student, ParentRelationship.Mother, verify: false);

        await using var context = _database.CreateContext();
        var service = Service(context);
        foreach (var who in new[] { parent, student })
        {
            var view = (await service.ListAsync(who.Id, default)).Value.Should().ContainSingle().Subject;
            view.Id.Should().Be(link);
            view.Products.Should().Equal(
                new ParentLinkProductView("Coaching", true, true),
                new ParentLinkProductView("SpeedReading", false, true));
        }
    }

    [Fact]
    public async Task OnlyTheStudentCanChooseWhatTheParentSeesOnAnOpenLink()
    {
        var parent = await AddUserAsync("veli@example.test", "Ayşe", _parentRole);
        var student = await AddUserAsync("ogrenci@example.test", "Ada", _studentRole, PlatformProduct.Coaching, PlatformProduct.SpeedReading);
        var otherStudent = await AddUserAsync("diger@example.test", "Can", _studentRole, PlatformProduct.Coaching);
        var pending = await LinkAsync(parent, student, ParentRelationship.Father, verify: false);

        await using (var context = _database.CreateContext())
        {
            var service = Service(context);
            (await service.SetProductVisibilityAsync(parent.Id, pending, false, false, default)).Error.Code.Should().Be("ParentLink.NotFound");
            (await service.SetProductVisibilityAsync(otherStudent.Id, pending, false, false, default)).Error.Code.Should().Be("ParentLink.NotFound");
            (await service.SetProductVisibilityAsync(student.Id, Guid.NewGuid(), false, false, default)).Error.Code.Should().Be("ParentLink.NotFound");
            (await service.SetProductVisibilityAsync(student.Id, pending, false, true, default)).IsSuccess.Should().BeTrue("a pending link can be set up before approval");
        }

        await using (var context = _database.CreateContext())
        {
            var row = await context.ParentStudentRelationships.AsNoTracking().SingleAsync(x => x.Id == pending);
            (row.CoachingVisible, row.SpeedReadingVisible).Should().Be((false, true));
            (await Service(context).ListAsync(parent.Id, default)).Value.Single().Products.Should().Equal(
                new ParentLinkProductView("Coaching", true, false),
                new ParentLinkProductView("SpeedReading", true, true));
            (await Service(context).RevokeAsync(student.Id, pending, default)).IsSuccess.Should().BeTrue();
        }

        await using (var context = _database.CreateContext())
            (await Service(context).SetProductVisibilityAsync(student.Id, pending, true, true, default)).Error.Code
                .Should().Be("ParentLink.NotFound", "an ended link cannot change");
    }

    [Fact]
    public async Task AParentMayReadAProductOnlyThroughAVerifiedRepresentativeLinkTheChildLeftVisible()
    {
        var parent = await AddUserAsync("veli@example.test", "Ayşe", _parentRole);
        var student = await AddUserAsync("ogrenci@example.test", "Ada", _studentRole, PlatformProduct.Coaching);
        var link = await LinkAsync(parent, student, ParentRelationship.Guardian, verify: true);

        await using (var context = _database.CreateContext())
        {
            var service = Service(context);
            (await service.AuthorizeReadAsync(parent.Id, student.Id, PlatformProduct.Coaching, default))
                .Should().Be(new ParentReadAuthorization(link, "Guardian"));
            (await service.AuthorizeReadAsync(parent.Id, student.Id, PlatformProduct.SpeedReading, default))
                .Should().BeNull("the child does not use speed reading");
            (await service.AuthorizeReadAsync(parent.Id, student.Id, PlatformProduct.CourseManagement, default)).Should().BeNull();
            (await service.AuthorizeReadAsync(student.Id, parent.Id, PlatformProduct.Coaching, default)).Should().BeNull();
            (await service.SetProductVisibilityAsync(student.Id, link, false, true, default)).IsSuccess.Should().BeTrue();
        }

        await using (var context = _database.CreateContext())
            (await Service(context).AuthorizeReadAsync(parent.Id, student.Id, PlatformProduct.Coaching, default))
                .Should().BeNull("the child hid Coaching from the parent");
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("other-relationship")]
    [InlineData("inactive-student")]
    [InlineData("inactive-parent")]
    [InlineData("access-revoked")]
    [InlineData("revoked-link")]
    public async Task ReadAuthorizationIsRefusedWhenAnyConditionFails(string failure)
    {
        var parent = await AddUserAsync("veli@example.test", "Ayşe", _parentRole);
        var student = await AddUserAsync("ogrenci@example.test", "Ada", _studentRole, PlatformProduct.SpeedReading);
        var link = await LinkAsync(parent, student, failure == "other-relationship" ? ParentRelationship.Other : ParentRelationship.Mother,
            verify: failure != "pending");
        await using (var context = _database.CreateContext())
        {
            var users = await context.Users.Include(x => x.ProductAccesses).Where(x => x.Id == parent.Id || x.Id == student.Id).ToDictionaryAsync(x => x.Id);
            if (failure == "inactive-student") users[student.Id].Deactivate();
            if (failure == "inactive-parent") users[parent.Id].Deactivate();
            if (failure == "access-revoked") users[student.Id].RevokeProductAccess(PlatformProduct.SpeedReading, Now.AddMinutes(5));
            if (failure == "revoked-link")
                (await context.ParentStudentRelationships.SingleAsync(x => x.Id == link)).Revoke(parent.Id, Now.UtcDateTime.AddMinutes(5), "Kaldırıldı.");
            await context.SaveChangesAsync();
        }

        await using var check = _database.CreateContext();
        (await Service(check).AuthorizeReadAsync(parent.Id, student.Id, PlatformProduct.SpeedReading, default)).Should().BeNull(failure);
    }

    [Fact]
    public async Task WeeklySummaryRecipientsCarryTheProductFlagsAndArePagedInTheDatabase()
    {
        var both = await AddUserAsync("both@example.test", "İki", _studentRole, PlatformProduct.Coaching, PlatformProduct.SpeedReading);
        var coachingOnly = await AddUserAsync("coaching@example.test", "Koç", _studentRole, PlatformProduct.Coaching);
        var hidden = await AddUserAsync("hidden@example.test", "Gizli", _studentRole, PlatformProduct.Coaching);
        var noProduct = await AddUserAsync("none@example.test", "Hiç", _studentRole);
        var parent = await AddUserAsync("veli@example.test", "Ayşe", _parentRole);
        var muted = await AddUserAsync("sessiz@example.test", "Can", _parentRole);
        await using (var context = _database.CreateContext())
        {
            context.ParentProfiles.Add(ParentProfile.Create(parent.Id, "Ayşe", "Veli", ParentRelationship.Mother));
            var mutedProfile = ParentProfile.Create(muted.Id, "Can", "Veli", ParentRelationship.Father);
            mutedProfile.UpdateNotificationPreferences("{\"dailyReport\": true, \"weeklyProgress\": false}");
            context.ParentProfiles.Add(mutedProfile);
            await context.SaveChangesAsync();
        }
        var bothLink = await LinkAsync(parent, both, ParentRelationship.Mother, verify: true);
        await LinkAsync(parent, coachingOnly, ParentRelationship.Mother, verify: true);
        var hiddenLink = await LinkAsync(parent, hidden, ParentRelationship.Mother, verify: true);
        await LinkAsync(parent, noProduct, ParentRelationship.Mother, verify: true);
        await LinkAsync(muted, both, ParentRelationship.Father, verify: true);
        await using (var context = _database.CreateContext())
        {
            (await Service(context).SetProductVisibilityAsync(hidden.Id, hiddenLink, false, true, default)).IsSuccess.Should().BeTrue();
            (await Service(context).SetProductVisibilityAsync(both.Id, bothLink, true, false, default)).IsSuccess.Should().BeTrue();
        }

        await using var read = _database.CreateContext();
        var service = Service(read);
        var all = await service.GetWeeklySummaryRecipientsAsync(1, 50, default);
        all.TotalCount.Should().Be(2, "no product left, a hidden-only child and a muted parent are skipped");
        all.Items.Select(x => (x.StudentUserId, x.CoachingVisible, x.SpeedReadingVisible, x.StudentHasCoaching, x.StudentHasSpeedReading))
            .Should().BeEquivalentTo([(both.Id, true, false, true, true), (coachingOnly.Id, true, true, true, false)]);
        all.Items.Should().OnlyContain(x => x.ParentUserId == parent.Id && x.ParentEmail == "veli@example.test" && x.ParentName == "Ayşe Veli");

        var first = await service.GetWeeklySummaryRecipientsAsync(1, 1, default);
        var second = await service.GetWeeklySummaryRecipientsAsync(2, 1, default);
        var third = await service.GetWeeklySummaryRecipientsAsync(3, 1, default);
        (first.TotalCount, second.TotalCount, third.TotalCount).Should().Be((2, 2, 2));
        first.Items.Concat(second.Items).Select(x => x.StudentUserId).Should().BeEquivalentTo([both.Id, coachingOnly.Id]);
        third.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifiedLinksForCoachingAreOnlyRepresentativeRelationships()
    {
        var parent = await AddUserAsync("veli@example.test", "Ayşe", _parentRole);
        var other = await AddUserAsync("diger@example.test", "Can", _parentRole);
        var student = await AddUserAsync("ogrenci@example.test", "Ada", _studentRole, PlatformProduct.Coaching);
        await LinkAsync(parent, student, ParentRelationship.Mother, verify: true);
        await LinkAsync(other, student, ParentRelationship.Other, verify: true);

        await using var context = _database.CreateContext();
        (await Service(context).GetVerifiedLinksAsync([student.Id], null, default)).Should().ContainSingle()
            .Which.ParentUserId.Should().Be(parent.Id);
        (await Service(context).GetVerifiedLinksAsync([], other.Id, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task ExistingLinksStartVisibleForBothProducts()
    {
        var parent = await AddUserAsync("veli@example.test", "Ayşe", _parentRole);
        var student = await AddUserAsync("ogrenci@example.test", "Ada", _studentRole);
        var id = Guid.NewGuid();
        await using (var context = _database.CreateContext())
        {
            // A row written as before the migration: the new columns are not named, the database fills them in.
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO identity.parent_student_relationships
                    (id, parent_user_id, student_user_id, relationship, status, requested_by_user_id, requested_at, created_at, version)
                VALUES ({id}, {parent.Id}, {student.Id}, 'Mother', 'Verified', {parent.Id}, {Now.UtcDateTime}, {Now.UtcDateTime}, 0)
                """);
        }

        await using var read = _database.CreateContext();
        var row = await read.ParentStudentRelationships.AsNoTracking().SingleAsync(x => x.Id == id);
        (row.CoachingVisible, row.SpeedReadingVisible).Should().Be((true, true));
    }

    private ParentLinkService Service(IdentityDbContext context) => new(context, new FixedClock());

    private async Task<User> AddUserAsync(string email, string firstName, Role role, params PlatformProduct[] products)
    {
        var user = User.Create(Guid.NewGuid(), email, firstName, role == _studentRole ? "Öğrenci" : "Veli");
        user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id));
        foreach (var product in products) user.GrantProductAccess(product, UserProductAccessSource.Admin, null, Now.AddDays(-1));
        await using var context = _database.CreateContext();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<Guid> LinkAsync(User parent, User student, ParentRelationship relationship, bool verify)
    {
        var link = ParentStudentRelationship.Request(parent.Id, student.Id, relationship, parent.Id, Now.UtcDateTime.AddMinutes(-10));
        if (verify) link.Verify(ParentStudentVerificationMethod.InvitationAcceptance, student.Id, Now.UtcDateTime.AddMinutes(-5));
        await using var context = _database.CreateContext();
        context.ParentStudentRelationships.Add(link);
        await context.SaveChangesAsync();
        return link.Id;
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
