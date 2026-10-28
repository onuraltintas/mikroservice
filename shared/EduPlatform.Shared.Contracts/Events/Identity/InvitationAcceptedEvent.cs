namespace EduPlatform.Shared.Contracts.Events.Identity;

/// <summary>Roadmap O8: an invitation was accepted; the inviter is told who joined.</summary>
public sealed record InvitationAcceptedEvent(
    Guid InvitationId,
    Guid InviterId,
    Guid InviteeId,
    string InviteeName,
    string InvitationType,
    DateTime AcceptedAt)
{
    public string? Product { get; init; }
}
