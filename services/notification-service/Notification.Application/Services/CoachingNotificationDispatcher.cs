using System.Security.Cryptography;
using System.Text;
using Notification.Application.Interfaces;

namespace Notification.Application.Services;

public sealed class CoachingNotificationDispatcher : ICoachingNotificationDispatcher
{
    private const string SourceIdPrefix = "coaching-notification:";
    private readonly INotificationService _notificationService;

    public CoachingNotificationDispatcher(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task SendAsync(
        Guid eventMessageId,
        IReadOnlyCollection<Guid> recipientIds,
        string title,
        string message,
        string type,
        string relatedEntityId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientIds);

        foreach (var recipientId in recipientIds.Where(id => id != Guid.Empty).Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _notificationService.SendNotificationAsync(
                recipientId,
                title,
                message,
                type,
                relatedEntityId,
                CreateRecipientMessageId(eventMessageId, recipientId));
        }
    }

    internal static Guid CreateRecipientMessageId(Guid eventMessageId, Guid recipientId)
    {
        var canonical = $"{SourceIdPrefix}{eventMessageId:N}:{recipientId:N}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return new Guid(hash.AsSpan(0, 16));
    }
}
