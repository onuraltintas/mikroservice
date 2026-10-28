namespace Notification.Application.Configuration;

/// <summary>Where platform-level Coaching subscription alerts (new bank-transfer notices, institution expiries) are e-mailed.</summary>
public sealed class CoachingNoticeOptions
{
    public const string SectionName = "CoachingNotices";

    /// <summary>Empty disables the admin e-mails; the payer notices are unaffected.</summary>
    public string? AdminAlertEmail { get; set; }
}
