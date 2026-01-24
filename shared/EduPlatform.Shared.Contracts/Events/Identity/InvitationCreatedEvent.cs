namespace EduPlatform.Shared.Contracts.Events.Identity;

public record InvitationCreatedEvent(
    Guid InvitationId,
    string InviterEmail,
    string InviteeEmail,
    Guid? InviteeId,
    string InvitationType,
    string? Message,
    string? Link,
    DateTime CreatedAt
);
