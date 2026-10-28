using Identity.Application.Notifications;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Roadmap O8: the notification service asks Identity for names and e-mail addresses of the people it notifies.
public sealed class NotificationRecipientDirectoryTests
{
    [Fact]
    public async Task ReturnsNamesAndOnlyUsableAddressesOfActiveUsers()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var active = User.Create(Guid.NewGuid(), "Ada@Example.test", "Ada", "Yılmaz");
        var inactive = User.Create(Guid.NewGuid(), "can@example.test", "Can", "Demir"); inactive.Deactivate();
        var erased = User.Create(Guid.NewGuid(), "eda@example.test", "Eda", "Kaya"); erased.AnonymizeForErasure();
        db.AddRange(active, inactive, erased); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var directory = new NotificationRecipientDirectory(db);

        var contacts = await directory.GetAsync([active.Id, inactive.Id, erased.Id, Guid.NewGuid()], default);
        var ada = Assert.Single(contacts, x => x.UserId == active.Id);
        Assert.Equal(("ada@example.test", "Ada", "Yılmaz"), (ada.Email, ada.FirstName, ada.LastName));
        Assert.Null(Assert.Single(contacts, x => x.UserId == inactive.Id).Email);
        Assert.DoesNotContain(contacts, x => x.UserId == erased.Id && x.Email is not null);
        await Assert.ThrowsAsync<ArgumentException>(() => directory.GetAsync(Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray(), default));
    }

    [Fact]
    public void AcceptedInvitationTellsTheInviterWhoJoined()
    {
        var inviter = Guid.NewGuid(); var invitee = User.Create(Guid.NewGuid(), "ada@example.test", "Ada", "Yılmaz");
        var invitation = Invitation.Create(inviter, "ada@example.test", InvitationType.StudentToTeacher);
        invitation.Accept(invitee.Id);
        var message = InvitationAcceptedMessages.From(invitation, invitee);
        Assert.Equal((invitation.Id, inviter, invitee.Id, "Ada Yılmaz", "StudentToTeacher", "coaching"),
            (message.InvitationId, message.InviterId, message.InviteeId, message.InviteeName, message.InvitationType, message.Product));
    }
}
