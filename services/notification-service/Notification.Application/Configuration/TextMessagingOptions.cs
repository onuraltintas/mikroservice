namespace Notification.Application.Configuration;

/// <summary>
/// Roadmap O9: SMS / WhatsApp sending. Off until a provider (e.g. Netgsm, WhatsApp Business) is chosen and connected;
/// while off, opted-in messages are only recorded as suppressed (masked number, no text).
/// </summary>
public sealed class TextMessagingOptions
{
    public const string SectionName = "TextMessaging";
    public bool Enabled { get; set; }
    public string? Provider { get; set; }
}
