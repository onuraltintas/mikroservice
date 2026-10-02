using EduPlatform.Shared.Contracts.Events.Coaching;
using MassTransit;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public sealed class StudyPlanPublishedConsumer(ICoachingNotificationDispatcher dispatcher) : IConsumer<StudyPlanPublishedEvent>
{
    public Task Consume(ConsumeContext<StudyPlanPublishedEvent> context)
    {
        var message = context.Message;
        var messageId = context.MessageId ?? throw new InvalidOperationException("StudyPlanPublishedEvent.MessageId is required.");
        return dispatcher.SendAsync(messageId, [message.StudentId],
            "Çalışma planın hazır", message.Title, "StudyPlanPublished", message.RevisionId.ToString(), context.CancellationToken);
    }
}
