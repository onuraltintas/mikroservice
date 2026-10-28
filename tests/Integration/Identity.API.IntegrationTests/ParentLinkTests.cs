using FluentAssertions;
using Identity.API.Controllers;
using Identity.Application.ParentLinks;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using IdentityUserRole = Identity.Domain.Enums.UserRole;

namespace Identity.API.IntegrationTests;

// Roadmap Y7: a parent links to their child with a code the child creates; the child approves the link.
public sealed class ParentLinkTests
{
    [Fact]
    public async Task ParentLinksWithTheChildsCodeAndTheChildApproves()
    {
        await using var context = CreateContext(); var clock = new Clock();
        var (parent, student, _) = AddUsers(context); await context.SaveChangesAsync();
        var links = new ParentLinkService(context, clock);

        var issued = (await links.CreateCodeAsync(student.Id, default)).Value;
        issued.Code.Should().MatchRegex("^[A-Z2-9]{4}-[A-Z2-9]{4}$");
        issued.ExpiresAt.Should().Be(clock.GetUtcNow().UtcDateTime.AddDays(7));

        var requested = await links.RequestAsync(parent.Id, issued.Code.ToLowerInvariant().Replace("-", " "), ParentRelationship.Mother, default);
        requested.IsSuccess.Should().BeTrue();
        requested.Value.Status.Should().Be("Pending");
        requested.Value.StudentName.Should().Be("Ada Öğrenci");

        var studentView = (await links.ListAsync(student.Id, default)).Value.Should().ContainSingle().Subject;
        (studentView.ParentName, studentView.Relationship, studentView.Status).Should().Be(("Ayşe Veli", "Mother", "Pending"));
        (await links.ApproveAsync(student.Id, studentView.Id, default)).IsSuccess.Should().BeTrue();

        var row = await context.ParentStudentRelationships.SingleAsync();
        (row.Status, row.VerificationMethod, row.VerifiedByUserId).Should()
            .Be((ParentStudentRelationshipStatus.Verified, ParentStudentVerificationMethod.InvitationAcceptance, student.Id));
        (await new ParentStudentRelationshipRepository(context).AuthorizeCoachingAgreementRepresentativeAsync(parent.Id, student.Id, default))
            .Should().NotBeNull("a verified mother may read her child's Coaching data");
        (await links.ListAsync(parent.Id, default)).Value.Should().ContainSingle(x => x.StudentUserId == student.Id && x.Status == "Verified");
    }

    [Fact]
    public async Task CodesAreSingleUseExpireAndANewCodeReplacesTheOldOne()
    {
        await using var context = CreateContext(); var clock = new Clock();
        var (parent, student, otherParent) = AddUsers(context); await context.SaveChangesAsync();
        var links = new ParentLinkService(context, clock);

        var first = (await links.CreateCodeAsync(student.Id, default)).Value;
        var second = (await links.CreateCodeAsync(student.Id, default)).Value;
        (await links.RequestAsync(parent.Id, first.Code, ParentRelationship.Father, default)).Error.Code.Should().Be("ParentLink.InvalidCode");
        (await links.RequestAsync(parent.Id, second.Code, ParentRelationship.Father, default)).IsSuccess.Should().BeTrue();
        (await links.RequestAsync(otherParent.Id, second.Code, ParentRelationship.Guardian, default)).Error.Code.Should().Be("ParentLink.InvalidCode");

        var third = (await links.CreateCodeAsync(student.Id, default)).Value;
        (await links.RequestAsync(parent.Id, third.Code, ParentRelationship.Father, default)).Error.Code.Should().Be("ParentLink.AlreadyLinked");
        clock.Now = clock.Now.AddDays(8);
        (await links.RequestAsync(otherParent.Id, third.Code, ParentRelationship.Guardian, default)).Error.Code.Should().Be("ParentLink.InvalidCode");
    }

    [Fact]
    public async Task OnlyAParentCanUseACodeOnlyTheChildCanApproveAndEitherSideCanEndTheLink()
    {
        await using var context = CreateContext(); var clock = new Clock();
        var (parent, student, otherParent) = AddUsers(context); await context.SaveChangesAsync();
        var links = new ParentLinkService(context, clock);
        var code = (await links.CreateCodeAsync(student.Id, default)).Value.Code;

        (await links.CreateCodeAsync(parent.Id, default)).Error.Code.Should().Be("ParentLink.Forbidden");
        (await links.RequestAsync(student.Id, code, ParentRelationship.Mother, default)).Error.Code.Should().Be("ParentLink.Forbidden");
        (await links.RequestAsync(parent.Id, code, ParentRelationship.Other, default)).Error.Code.Should().Be("ParentLink.InvalidRelationship");
        var link = (await links.RequestAsync(parent.Id, code, ParentRelationship.Guardian, default)).Value;

        (await links.ApproveAsync(parent.Id, link.Id, default)).Error.Code.Should().Be("ParentLink.NotFound");
        (await links.RevokeAsync(otherParent.Id, link.Id, default)).Error.Code.Should().Be("ParentLink.NotFound");
        (await links.ApproveAsync(student.Id, link.Id, default)).IsSuccess.Should().BeTrue();
        (await links.ApproveAsync(student.Id, link.Id, default)).Error.Code.Should().Be("ParentLink.NotPending");
        (await links.RevokeAsync(parent.Id, link.Id, default)).IsSuccess.Should().BeTrue();

        (await context.ParentStudentRelationships.SingleAsync()).Status.Should().Be(ParentStudentRelationshipStatus.Revoked);
        (await links.ListAsync(student.Id, default)).Value.Should().BeEmpty("ended links leave the list");
    }

    [Fact]
    public async Task WeeklySummaryRecipientsAreVerifiedParentsWhoKeptTheWeeklyPreference()
    {
        await using var context = CreateContext(); var clock = new Clock();
        var (parent, student, otherParent) = AddUsers(context);
        var muted = ParentProfile.Create(otherParent.Id, "Can", "Veli", ParentRelationship.Father);
        muted.UpdateNotificationPreferences("{\"weeklyProgress\":false}");
        context.ParentProfiles.Add(muted); await context.SaveChangesAsync();
        var links = new ParentLinkService(context, clock);
        foreach (var who in new[] { parent, otherParent })
        {
            var code = (await links.CreateCodeAsync(student.Id, default)).Value.Code;
            var link = (await links.RequestAsync(who.Id, code, ParentRelationship.Guardian, default)).Value;
            await links.ApproveAsync(student.Id, link.Id, default);
        }

        var page = await links.GetWeeklySummaryRecipientsAsync(1, 50, default);

        var recipient = page.Items.Should().ContainSingle().Subject;
        (recipient.ParentUserId, recipient.ParentEmail, recipient.StudentUserId, recipient.StudentName)
            .Should().Be((parent.Id, "parent@example.test", student.Id, "Ada Öğrenci"));
    }

    [Theory]
    [InlineData(nameof(ParentLinksController.CreateCode), "Student")]
    [InlineData(nameof(ParentLinksController.RequestLink), "Parent")]
    [InlineData(nameof(ParentLinksController.Approve), "Student")]
    [InlineData(nameof(ParentLinksController.Revoke), "Student,Parent")]
    [InlineData(nameof(ParentLinksController.List), "Student,Parent")]
    public void SelfServiceEndpointsAreLimitedToTheTwoSides(string action, string roles) =>
        typeof(ParentLinksController).GetMethod(action)!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Should().Contain(x => x.Roles == roles);

    private static (User Parent, User Student, User OtherParent) AddUsers(IdentityDbContext context)
    {
        var parentRole = Role.Create(IdentityUserRole.Parent.ToString(), "Parent", true);
        var studentRole = Role.Create(IdentityUserRole.Student.ToString(), "Student", true);
        User Make(string email, string first, string last, Role role)
        {
            var user = User.Create(Guid.NewGuid(), email, first, last);
            user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id)); return user;
        }
        var parent = Make("parent@example.test", "Ayşe", "Veli", parentRole);
        var student = Make("student@example.test", "Ada", "Öğrenci", studentRole);
        var other = Make("other@example.test", "Can", "Veli", parentRole);
        context.AddRange(parentRole, studentRole, parent, student, other);
        return (parent, student, other);
    }

    private static IdentityDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
