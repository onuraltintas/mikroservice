using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Privacy;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Services;

public sealed class NotificationErasureAssessmentService(
    NotificationDbContext context,
    TimeProvider timeProvider) : INotificationErasureAssessmentService
{
    public async Task<NotificationErasureAssessment> AssessAsync(
        PersonalDataErasureAssessmentRequestedV1 message,
        CancellationToken cancellationToken)
    {
        var count = await context.Notifications.CountAsync(
            item => item.UserId == message.SubjectUserId,
            cancellationToken);
        return new NotificationErasureAssessment(
            message.RequestId,
            message.SubjectUserId,
            CanProceed: true,
            new Dictionary<string, int> { ["notifications"] = count },
            timeProvider.GetUtcNow().UtcDateTime);
    }
}
