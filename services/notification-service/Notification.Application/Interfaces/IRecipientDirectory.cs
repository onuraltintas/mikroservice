namespace Notification.Application.Interfaces;

/// <summary>Roadmap O8: names and e-mail addresses of notification recipients, from Identity.</summary>
public sealed record RecipientContact(Guid UserId, string? Email, string FirstName, string LastName)
{
    public string DisplayName => $"{FirstName} {LastName}".Trim();
}

public interface IRecipientDirectory
{
    Task<IReadOnlyDictionary<Guid, RecipientContact>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}

public sealed record NotificationChannels(bool InApp, bool Email);

/// <summary>Roadmap O8: which channels each recipient chose for a notification type, and the e-mail channel itself.</summary>
public interface INotificationChannelPolicy
{
    Task<IReadOnlyDictionary<Guid, NotificationChannels>> PlanAsync(IReadOnlyCollection<Guid> userIds, string type, CancellationToken ct);
    Task EmailAsync(Guid eventMessageId, IReadOnlyCollection<Guid> userIds, string title, string message, string type, CancellationToken ct);
}

public sealed record NotificationPreferenceView(string Category, string Label, bool InApp, bool Email);
public sealed record NotificationPreferenceUpdate(string Category, bool InApp, bool Email);

public interface INotificationPreferenceService
{
    Task<IReadOnlyList<NotificationPreferenceView>> GetAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<NotificationPreferenceView>> SaveAsync(Guid userId, IReadOnlyList<NotificationPreferenceUpdate> updates, CancellationToken ct);
}
