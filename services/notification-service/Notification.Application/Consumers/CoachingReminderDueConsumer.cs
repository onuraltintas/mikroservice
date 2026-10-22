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
        var overdue = reminder.Kind == "StudyOverdue";
        var fee = reminder.Kind == "FeeOverdue";
        if (reminder.Kind is not ("StudyDay" or "StudyOverdue" or "Session" or "FeeOverdue") || reminder.ReminderId == Guid.Empty || reminder.RecipientUserId == Guid.Empty)
            throw new InvalidOperationException("A coaching reminder requires a valid kind and delivery identity.");
        return dispatcher.SendAsync(reminder.ReminderId, [reminder.RecipientUserId],
            overdue ? "Geciken çalışmaların var" : fee ? "Koçluk ödeme hatırlatması" : study ? "Bugünkü çalışma planın" : "Yaklaşan koçluk seansı",
            overdue ? "Önceki günlerden tamamlanmamış çalışmaların var. Planını açarak tamamlayabilir veya çalışma zamanını düzenleyebilirsin."
                : fee ? "Vadesi geçen koçluk taksitin var. Ücretler ekranından güncel kaydını inceleyebilirsin."
                : study ? "Bugün için tamamlanmamış çalışmaların var. Çalışma planını öğrenci panelinden inceleyebilirsin."
                : "Koçluk seansın bir saat içinde başlayacak. Seanslar ekranından zamanı ve görüşme bağlantısını kontrol edebilirsin.",
            overdue ? "StudyPlanOverdueReminder" : fee ? "FeeOverdueReminder" : study ? "StudyPlanReminder" : "SessionReminder", reminder.SourceId.ToString(), context.CancellationToken);
    }
}
