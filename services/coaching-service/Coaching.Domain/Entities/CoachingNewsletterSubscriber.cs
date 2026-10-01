namespace Coaching.Domain.Entities;

public sealed class CoachingNewsletterSubscriber
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = "PendingConfirmation";
    public string Source { get; set; } = "CoachingWebsite";
    public string ConsentTextVersion { get; set; } = string.Empty;
    public DateTime ConsentedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? UnsubscribedAt { get; set; }
    public string? ConfirmationTokenHash { get; set; }
    public DateTime? ConfirmationTokenExpiresAt { get; set; }
    public string? UnsubscribeTokenHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
