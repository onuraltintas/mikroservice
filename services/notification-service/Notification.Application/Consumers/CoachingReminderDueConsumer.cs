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
        if (reminder.Kind is not ("StudyDay" or "StudyOverdue" or "Session" or "FeeOverdue" or "AssignmentDue" or "PlanEnding") || reminder.ReminderId == Guid.Empty || reminder.RecipientUserId == Guid.Empty)
            throw new InvalidOperationException("A coaching reminder requires a valid kind and delivery identity.");
        if (reminder.Kind == "AssignmentDue")
        {
            // Roadmap O8: one day before an assignment is due, for a student who has not handed it in.
            var due = Notification.Application.Services.CoachingNotificationTexts.AssignmentDue(reminder.SubjectTitle, reminder.DueAt);
            return dispatcher.SendAsync(reminder.ReminderId, [reminder.RecipientUserId], due.Title, due.Message, "AssignmentDueReminder",
                reminder.SourceId.ToString(), context.CancellationToken);
        }
        if (reminder.Kind == "PlanEnding")
            // User decision 15.11.2026: a plan made for a week (or up to a chosen date) is ending; whoever made it is told.
            // A teacher's reminder carries the student's name (SubjectTitle); the student's own does not.
            return dispatcher.SendAsync(reminder.ReminderId, [reminder.RecipientUserId], "Çalışma planı bitiyor",
                (string.IsNullOrWhiteSpace(reminder.SubjectTitle) ? "Hazırladığın çalışma planı" : $"{reminder.SubjectTitle.Trim()} için hazırladığın çalışma planı")
                    + " bitmek üzere ve devamı henüz hazır değil. Plan sayfasındaki \"Gelecek haftayı ekle\" ile devamını hazırlayabilirsin.",
                "StudyPlanEndingReminder", reminder.SourceId.ToString(), context.CancellationToken);
        return dispatcher.SendAsync(reminder.ReminderId, [reminder.RecipientUserId],
            overdue ? "Geciken çalışmaların var" : fee ? "Koçluk ödeme hatırlatması" : study ? "Bugünkü çalışma planın" : "Yaklaşan koçluk seansı",
            overdue ? "Önceki günlerden tamamlanmamış çalışmaların var. Planını açarak tamamlayabilir veya çalışma zamanını düzenleyebilirsin."
                : fee ? "Vadesi geçen koçluk taksitin var. Ücretler ekranından güncel kaydını inceleyebilirsin."
                : study ? "Bugün için tamamlanmamış çalışmaların var. Çalışma planını öğrenci panelinden inceleyebilirsin."
                : "Koçluk seansın bir saat içinde başlayacak. Seanslar ekranından zamanı ve görüşme bağlantısını kontrol edebilirsin.",
            overdue ? "StudyPlanOverdueReminder" : fee ? "FeeOverdueReminder" : study ? "StudyPlanReminder" : "SessionReminder", reminder.SourceId.ToString(), context.CancellationToken);
    }
}
