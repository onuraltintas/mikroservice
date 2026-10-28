namespace Notification.Domain.Entities;

/// <summary>
/// Roadmap O8: a user's choice for one notification category. A missing row means the category's defaults
/// (in the app always; by e-mail when the category is important).
/// </summary>
public sealed class NotificationPreference
{
    public Guid UserId { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public bool InApp { get; private set; }
    public bool Email { get; private set; }
    /// <summary>Roadmap O9: opt-in text channels; off unless the user chooses them.</summary>
    public bool Sms { get; private set; }
    public bool WhatsApp { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    private NotificationPreference() { }

    public static NotificationPreference Create(Guid userId, string category, bool inApp, bool email, DateTime now, bool sms = false, bool whatsApp = false)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(category) || category.Length > 40)
            throw new ArgumentException("Bildirim tercihi için kullanıcı ve kategori gerekli.");
        return new NotificationPreference { UserId = userId, Category = category, InApp = inApp, Email = email, UpdatedAt = now, Sms = sms, WhatsApp = whatsApp };
    }

    public void Update(bool inApp, bool email, DateTime now, bool sms = false, bool whatsApp = false) { InApp = inApp; Email = email; Sms = sms; WhatsApp = whatsApp; UpdatedAt = now; }
}
