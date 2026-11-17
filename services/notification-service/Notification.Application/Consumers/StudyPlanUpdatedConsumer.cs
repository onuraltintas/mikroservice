using EduPlatform.Shared.Contracts.Events.Coaching;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;
namespace Notification.Application.Consumers;
public sealed class StudyPlanUpdatedConsumer(ICoachingNotificationDispatcher dispatcher, INotificationDbContext? db = null, TimeProvider? clock = null):IConsumer<StudyPlanUpdatedEvent>
{
 /// <summary>Live test 16.11.2026 (#36), user decision 17.11.2026: a teacher's changes to one plan within this time are one notice.</summary>
 public static readonly TimeSpan MergeWindow = TimeSpan.FromMinutes(30);
 public const string MergedMessage = "Öğretmenin çalışma planında birkaç değişiklik yaptı. Güncel çalışma planını öğrenci panelinden inceleyebilirsin.";

 public async Task Consume(ConsumeContext<StudyPlanUpdatedEvent> context)
 {
  var message=context.Message;
  var messageId=context.MessageId??throw new InvalidOperationException("StudyPlanUpdatedEvent.MessageId is required.");
  if(db is not null)
  {
   var since=(clock??TimeProvider.System).GetUtcNow().UtcDateTime-MergeWindow;
   var revision=message.RevisionId.ToString();
   var open=await db.Notifications.Where(x=>x.UserId==message.StudentId&&x.Type=="StudyPlanUpdated"&&x.RelatedEntityId==revision&&!x.IsRead&&x.CreatedAt>=since)
    .OrderByDescending(x=>x.CreatedAt).FirstOrDefaultAsync(context.CancellationToken);
   if(open is not null)
   {
    open.Message=MergedMessage;
    await db.SaveChangesAsync(context.CancellationToken);
    return;
   }
  }
  await dispatcher.SendAsync(messageId,[message.StudentId],"Çalışma planın güncellendi",
   message.ChangeDescription+" Güncel çalışma planını öğrenci panelinden inceleyebilirsin.","StudyPlanUpdated",message.RevisionId.ToString(),context.CancellationToken);
 }
}
