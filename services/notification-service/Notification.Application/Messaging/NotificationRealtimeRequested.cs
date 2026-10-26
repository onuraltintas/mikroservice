namespace Notification.Application.Messaging;

/// <summary>
/// Internal message: push a stored notification to the recipient's open connections.
/// It travels through the EF outbox, so it is only delivered once the notification row
/// has committed; a rolled-back notification is never shown.
/// </summary>
public sealed record NotificationRealtimeRequested(Guid NotificationId, Guid UserId);
