namespace EduPlatform.Shared.Contracts.Events.Identity;

/// <summary>
/// V1 (parent panel): tells the other side of a parent link what happened (in the app and by e-mail).
/// Identity decides the recipient and where the link points (<see cref="LinkProduct"/> is "coaching" or
/// "speed-reading", <see cref="LinkPath"/> is a path on that product's public site); Notification writes the copy.
/// <see cref="EventId"/> is stable per link, change and recipient, so a retried request cannot notify twice.
/// </summary>
public sealed record ParentLinkNotificationV1(
    Guid EventId,
    Guid LinkId,
    string Change,
    Guid RecipientUserId,
    string RecipientRole,
    string? RecipientEmail,
    string RecipientFirstName,
    string OtherPartyName,
    string LinkProduct,
    string LinkPath,
    DateTime OccurredAt);

public static class ParentLinkChanges
{
    /// <summary>A parent used the student's code; the student is asked to approve.</summary>
    public const string Requested = "Requested";
    /// <summary>The student approved; the parent is told.</summary>
    public const string Approved = "Approved";
    /// <summary>One side removed the link (or turned the request down); the other side is told.</summary>
    public const string Revoked = "Revoked";
}
