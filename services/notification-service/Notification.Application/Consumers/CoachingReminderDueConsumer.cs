using EduPlatform.Shared.Contracts.Events.Coaching;
using MassTransit;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public sealed class CoachingReminderDueConsumer(ICoachingNotificationDispatcher dispatcher) : IConsumer<CoachingReminderDueEvent>
{
    public Task Consume(ConsumeContext<CoachingReminderDueEvent> context)
    {
        var reminder = context.Message;
        var study = reminder.Kind == "StudyDay";
        if (reminder.Kind is not ("StudyDay" or "Session") || reminder.ReminderId == Guid.Empty || reminder.RecipientUserId == Guid.Empty)
            throw new InvalidOperationException("A coaching reminder requires a valid kind and delivery identity.");
        return dispatcher.SendAsync(reminder.ReminderId, [reminder.RecipientUserId],
            study ? "Bugünkü çalışma planın" : "Yaklaşan koçluk seansı",
            study ? "Bugün için tamamlanmamış çalışmaların var. Çalışma planını öğrenci panelinden inceleyebilirsin."
                : "Koçluk seansın bir saat içinde başlayacak. Seanslar ekranından zamanı ve görüşme bağlantısını kontrol edebilirsin.",
            study ? "StudyPlanReminder" : "SessionReminder", reminder.SourceId.ToString(), context.CancellationToken);
    }
}
