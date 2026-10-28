using Notification.Application.Interfaces;
using Notification.Application.Messaging;

namespace Notification.Application.Services;

/// <summary>
/// Fans one event out to its recipients. Each recipient gets a message id derived from the
/// event id, so a redelivered event cannot notify anyone twice. The prefix keeps the ids of
/// different products apart and must not change.
/// Roadmap O8: with a channel policy each recipient's own choices decide whether the note
/// appears in the app and whether it is also e-mailed.
/// </summary>
public abstract class RecipientNotificationDispatcher(INotificationService notificationService, string sourceIdPrefix, INotificationChannelPolicy? channels = null)
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
        var recipients = recipientIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (recipients.Length == 0) return;
        var plan = channels is null ? null : await channels.PlanAsync(recipients, type, cancellationToken);
        foreach (var recipientId in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (plan is not null && plan.TryGetValue(recipientId, out var chosen) && !chosen.InApp) continue;
            await notificationService.SendNotificationAsync(recipientId, title, message, type, relatedEntityId,
                RecipientMessageId(eventMessageId, recipientId));
        }
        var email = plan?.Where(x => x.Value.Email).Select(x => x.Key).ToArray() ?? [];
        if (email.Length > 0) await channels!.EmailAsync(eventMessageId, email, title, message, type, cancellationToken);
        // Roadmap O9: SMS / WhatsApp copies for those who opted in.
        if (plan is not null && plan.Values.Any(x => x.Sms || x.WhatsApp))
            await channels!.TextAsync(eventMessageId, plan.Where(x => x.Value.Sms || x.Value.WhatsApp).ToDictionary(x => x.Key, x => x.Value),
                title, message, type, cancellationToken);
    }

    public Guid RecipientMessageId(Guid eventMessageId, Guid recipientId) =>
        MessageIds.Derive($"{sourceIdPrefix}{eventMessageId:N}:{recipientId:N}");
}

public sealed class CoachingNotificationDispatcher(INotificationService notificationService, INotificationChannelPolicy? channels = null)
    : RecipientNotificationDispatcher(notificationService, "coaching-notification:", channels), ICoachingNotificationDispatcher;

public sealed class CourseManagementNotificationDispatcher(INotificationService notificationService)
    : RecipientNotificationDispatcher(notificationService, "course-management-notification:"), ICourseManagementNotificationDispatcher;
