namespace SpeedReading.Application.Subscription;

/// <summary>One subscription event somebody is told about: the payer (in-app and by e-mail) or, without a user, the platform admin.</summary>
public sealed record SpeedReadingSubscriptionNotice(
    Guid? UserId,
    string? Email,
    string Kind,
    string PlanName,
    DateTime? EndsAt,
    string? Detail,
    DateTime OccurredAt,
    /// <summary>Set by the lifecycle worker so the same reminder is never sent twice; otherwise a new id.</summary>
    Guid? NoticeId = null);

/// <summary>
/// Hızlı Okuma billing parity (14.11.2026): payment decisions, withdrawal answers and expiry reach the payer as they do in
/// Koçluk. A notice is only staged here; it becomes durable with the caller's own save, so a failed operation tells nobody.
/// </summary>
public interface ISpeedReadingSubscriptionNotices
{
    Task StageAsync(SpeedReadingSubscriptionNotice notice, CancellationToken cancellationToken = default);
}
