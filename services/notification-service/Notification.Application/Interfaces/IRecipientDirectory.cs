namespace Notification.Application.Interfaces;

/// <summary>Roadmap O8: names and e-mail addresses of notification recipients, from Identity.</summary>
public sealed record RecipientContact(Guid UserId, string? Email, string FirstName, string LastName)
{
    /// <summary>Roadmap O9: a confirmed mobile number in E.164 form, when the user has one.</summary>
    public string? Phone { get; init; }
    public string DisplayName => $"{FirstName} {LastName}".Trim();
}

public interface IRecipientDirectory
{
    Task<IReadOnlyDictionary<Guid, RecipientContact>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}

public sealed record NotificationChannels(bool InApp, bool Email, bool Sms = false, bool WhatsApp = false);

/// <summary>Roadmap O8: which channels each recipient chose for a notification type, and the e-mail channel itself.</summary>
public interface INotificationChannelPolicy
{
    Task<IReadOnlyDictionary<Guid, NotificationChannels>> PlanAsync(IReadOnlyCollection<Guid> userIds, string type, CancellationToken ct);
    Task EmailAsync(Guid eventMessageId, IReadOnlyCollection<Guid> userIds, string title, string message, string type, CancellationToken ct);
    /// <summary>Roadmap O9: SMS / WhatsApp copies for the users who opted in (channel per user from PlanAsync).</summary>
    Task TextAsync(Guid eventMessageId, IReadOnlyDictionary<Guid, NotificationChannels> plan, string title, string message, string type, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Roadmap O9: the SMS / WhatsApp outbox.</summary>
public interface ITextMessageQueue
{
    Task QueueAsync(Guid messageId, string channel, string consumerType, string recipient, string body, Guid? subjectUserId, CancellationToken ct);
}

public sealed record CommercialConsentView(string Channel, string Label, bool Granted, string IysStatus, DateTime? ChangedAt);
public sealed record CommercialConsentUpdate(string Channel, bool Granted);

public interface ICommercialConsentService
{
    Task<IReadOnlyList<CommercialConsentView>> GetAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<CommercialConsentView>> SaveAsync(Guid userId, IReadOnlyList<CommercialConsentUpdate> updates, CancellationToken ct);
}

public sealed record NotificationPreferenceView(string Category, string Label, bool InApp, bool Email, bool EmailEditable = true, bool TextAvailable = false, bool Sms = false, bool WhatsApp = false);
public sealed record NotificationPreferenceUpdate(string Category, bool InApp, bool Email)
{
    public bool Sms { get; init; }
    public bool WhatsApp { get; init; }
}

public interface INotificationPreferenceService
{
    Task<IReadOnlyList<NotificationPreferenceView>> GetAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<NotificationPreferenceView>> SaveAsync(Guid userId, IReadOnlyList<NotificationPreferenceUpdate> updates, CancellationToken ct);
}
