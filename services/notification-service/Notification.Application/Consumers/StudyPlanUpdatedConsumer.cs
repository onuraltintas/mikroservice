using EduPlatform.Shared.Contracts.Events.Coaching;
using MassTransit;
using Notification.Application.Interfaces;
namespace Notification.Application.Consumers;
public sealed class StudyPlanUpdatedConsumer(ICoachingNotificationDispatcher dispatcher):IConsumer<StudyPlanUpdatedEvent>
{
 public Task Consume(ConsumeContext<StudyPlanUpdatedEvent> context)
 {
  var message=context.Message;
  var messageId=context.MessageId??throw new InvalidOperationException("StudyPlanUpdatedEvent.MessageId is required.");
  return dispatcher.SendAsync(messageId,[message.StudentId],"Çalışma planın güncellendi",
   message.ChangeDescription+" Güncel çalışma planını öğrenci panelinden inceleyebilirsin.","StudyPlanUpdated",message.RevisionId.ToString(),context.CancellationToken);
 }
}
