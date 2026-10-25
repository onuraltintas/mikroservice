using EduPlatform.Shared.Contracts.Events.Coaching;
using MassTransit;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public sealed class WeeklyReflectionSavedConsumer(ICoachingNotificationDispatcher dispatcher) : IConsumer<WeeklyReflectionSavedEvent>
{
    public Task Consume(ConsumeContext<WeeklyReflectionSavedEvent> context)
    {
        var message = context.Message;
        return dispatcher.SendAsync(context.MessageId ?? throw new InvalidOperationException("Weekly reflection MessageId is required."),
            message.TeacherIds, "Haftalık değerlendirme kaydedildi", $"Öğrencinin {message.WeekStart:dd.MM.yyyy} haftasına ait değerlendirmesini inceleyebilirsin.",
            "WeeklyReflectionSaved", $"weekly:{message.StudentId:D}:{message.WeekStart:yyyy-MM-dd}", context.CancellationToken);
    }
}
public sealed class WeeklyDecisionChangedConsumer(ICoachingNotificationDispatcher dispatcher) : IConsumer<WeeklyDecisionChangedEvent>
{
    public Task Consume(ConsumeContext<WeeklyDecisionChangedEvent> context)
    {
        var message = context.Message;
        return dispatcher.SendAsync(context.MessageId ?? throw new InvalidOperationException("Weekly decision MessageId is required."),
            message.ResponsibilityTransferred ? [message.StudentId,message.TeacherId] : [message.StudentId], message.ResponsibilityTransferred ? "Haftalık karar sorumluluğu devredildi" : message.Created ? "Yeni haftalık karar" : "Haftalık karar güncellendi", "Kararı, sorumluluk geçmişini ve güncel durumunu haftalık değerlendirme bölümünden inceleyebilirsin.",
            message.Created ? "WeeklyDecisionCreated" : "WeeklyDecisionUpdated", $"weekly:{message.StudentId:D}:{message.WeekStart:yyyy-MM-dd}:{message.DecisionId:D}", context.CancellationToken);
    }
}
