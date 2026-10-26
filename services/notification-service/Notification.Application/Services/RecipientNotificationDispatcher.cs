using Notification.Application.Interfaces;
using Notification.Application.Messaging;

namespace Notification.Application.Services;

/// <summary>
/// Fans one event out to its recipients. Each recipient gets a message id derived from the
/// event id, so a redelivered event cannot notify anyone twice. The prefix keeps the ids of
/// different products apart and must not change.
/// </summary>
public abstract class RecipientNotificationDispatcher(INotificationService notificationService, string sourceIdPrefix)
{
    public async Task SendAsync(
        Guid eventMessageId,
        IReadOnlyCollection<Guid> recipientIds,
        string title,
        string message,
        string type,
        string? relatedEntityId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientIds);
        foreach (var recipientId in recipientIds.Where(id => id != Guid.Empty).Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await notificationService.SendNotificationAsync(recipientId, title, message, type, relatedEntityId,
                RecipientMessageId(eventMessageId, recipientId));
        }
    }

    public Guid RecipientMessageId(Guid eventMessageId, Guid recipientId) =>
        MessageIds.Derive($"{sourceIdPrefix}{eventMessageId:N}:{recipientId:N}");
}

public sealed class CoachingNotificationDispatcher(INotificationService notificationService)
    : RecipientNotificationDispatcher(notificationService, "coaching-notification:"), ICoachingNotificationDispatcher;

public sealed class CourseManagementNotificationDispatcher(INotificationService notificationService)
    : RecipientNotificationDispatcher(notificationService, "course-management-notification:"), ICourseManagementNotificationDispatcher;
