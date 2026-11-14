using EduPlatform.Shared.Contracts.Events.SpeedReading;
using MassTransit;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Subscription;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Infrastructure.Legacy;

/// <summary>
/// Writes the payer's in-app notification into the Speed Reading store and hands the e-mail to the notification service
/// through the bus outbox. Both ride on the caller's save.
/// </summary>
internal sealed class SpeedReadingSubscriptionNotices(ISpeedReadingDataContext db, IPublishEndpoint publishEndpoint) : ISpeedReadingSubscriptionNotices
{
    public const string SubscriptionPage = "/student/subscription";
    private const int SystemType = 9;
    private const int NormalPriority = 2;

    public async Task StageAsync(SpeedReadingSubscriptionNotice notice, CancellationToken cancellationToken = default)
    {
        var id = notice.NoticeId ?? Guid.NewGuid();
        var forAdmin = SpeedReadingSubscriptionNoticeKinds.IsForAdmin(notice.Kind);
        if (!forAdmin && notice.UserId is { } userId && userId != Guid.Empty)
        {
            var ends = notice.EndsAt is { } end ? SpeedReadingReportCalendar.LocalTime(end).ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) : null;
            var text = SpeedReadingSubscriptionNoticeTexts.Describe(notice.Kind, notice.PlanName, ends, notice.Detail)
                ?? throw new InvalidOperationException($"Unknown subscription notice kind '{notice.Kind}'.");
            db.Notifications.Add(new LegacyUserNotification
            {
                Id = id,
                UserId = userId,
                Title = text.Title,
                Message = text.Message.Length <= 1_000 ? text.Message : text.Message[..1_000],
                Type = SystemType,
                Priority = NormalPriority,
                Channel = 3,
                Status = 2,
                ActionUrl = SubscriptionPage,
                SentAt = notice.OccurredAt,
                CreatedAt = notice.OccurredAt
            });
        }

        await publishEndpoint.Publish(new SpeedReadingSubscriptionNoticeV1(id, forAdmin ? null : notice.UserId,
            forAdmin || string.IsNullOrWhiteSpace(notice.Email) ? null : notice.Email.Trim(), notice.Kind, notice.PlanName,
            notice.EndsAt, notice.Detail, notice.OccurredAt), cancellationToken);
    }
}
