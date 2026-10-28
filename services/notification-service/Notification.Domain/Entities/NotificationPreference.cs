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
    public DateTime UpdatedAt { get; private set; }
    private NotificationPreference() { }

    public static NotificationPreference Create(Guid userId, string category, bool inApp, bool email, DateTime now)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(category) || category.Length > 40)
            throw new ArgumentException("Bildirim tercihi için kullanıcı ve kategori gerekli.");
        return new NotificationPreference { UserId = userId, Category = category, InApp = inApp, Email = email, UpdatedAt = now };
    }

    public void Update(bool inApp, bool email, DateTime now) { InApp = inApp; Email = email; UpdatedAt = now; }
}
