using EduPlatform.Shared.Contracts.Events.Identity;
using Identity.Domain.Entities;

namespace Identity.Application.Notifications;

/// <summary>Roadmap O8: names and usable e-mail addresses of notification recipients (inactive accounts get no address).</summary>
public sealed record NotificationRecipient(Guid UserId, string? Email, string FirstName, string LastName);

public interface INotificationRecipientDirectory
{
    public const int MaxUsers = 200;
    Task<IReadOnlyList<NotificationRecipient>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}

/// <summary>Roadmap O8: the message telling the inviter that an invitation was accepted.</summary>
public static class InvitationAcceptedMessages
{
    public static InvitationAcceptedEvent From(Invitation invitation, User invitee) =>
        new(invitation.Id, invitation.InviterId, invitee.Id, $"{invitee.FirstName} {invitee.LastName}".Trim(), invitation.Type.ToString(),
            invitation.RespondedAt ?? DateTime.UtcNow) { Product = "coaching" };
}
