using EduPlatform.Shared.Contracts.Events.Coaching;
using MassTransit;
using Notification.Application.Messaging;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public sealed class StudyPlanPublishedConsumer(ICoachingNotificationDispatcher dispatcher) : IConsumer<StudyPlanPublishedEvent>
{
    public Task Consume(ConsumeContext<StudyPlanPublishedEvent> context)
    {
        var message = context.Message;
        // Live test 16.11.2026 (#12), user decision 17.11.2026: a plan the student published themselves needs no notice.
        if (message.PublishedBy == message.StudentId) return Task.CompletedTask;
        var messageId = MessageIds.Require(context);
        return dispatcher.SendAsync(messageId, [message.StudentId],
            "Çalışma planın hazır", "Çalışma planını öğrenci panelinden inceleyebilirsin.", "StudyPlanPublished", message.RevisionId.ToString(), context.CancellationToken);
    }
}
