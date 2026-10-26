using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public sealed class CourseManagementLessonScheduledConsumer(ICourseManagementNotificationDispatcher dispatcher)
    : IConsumer<CourseManagementLessonScheduledV1>
{
    public Task Consume(ConsumeContext<CourseManagementLessonScheduledV1> context)
    {
        var message = context.Message;
        return CourseManagementConsumerMessageIds.DispatchAsync(context, () => dispatcher.SendAsync(
            CourseManagementConsumerMessageIds.RequireMessageId(context), message.StudentUserIds,
            "Yeni ders planlandı", $"Dersin {TurkeyTime.Format(message.StartsAt)} tarihinde planlandı.",
            "CourseManagement.LessonScheduled", message.LessonId.ToString(), context.CancellationToken));
    }
}

public sealed class CourseManagementLessonRescheduledConsumer(ICourseManagementNotificationDispatcher dispatcher)
    : IConsumer<CourseManagementLessonRescheduledV1>
{
    public Task Consume(ConsumeContext<CourseManagementLessonRescheduledV1> context)
    {
        var message = context.Message;
        return CourseManagementConsumerMessageIds.DispatchAsync(context, () => dispatcher.SendAsync(
            CourseManagementConsumerMessageIds.RequireMessageId(context), message.StudentUserIds,
            "Ders saati değişti", $"Dersin yeni saati: {TurkeyTime.Format(message.StartsAt)}.",
            "CourseManagement.LessonRescheduled", message.LessonId.ToString(), context.CancellationToken));
    }
}

public sealed class CourseManagementLessonCancelledConsumer(ICourseManagementNotificationDispatcher dispatcher)
    : IConsumer<CourseManagementLessonCancelledV1>
{
    public Task Consume(ConsumeContext<CourseManagementLessonCancelledV1> context)
    {
        var message = context.Message;
        return CourseManagementConsumerMessageIds.DispatchAsync(context, () => dispatcher.SendAsync(
            CourseManagementConsumerMessageIds.RequireMessageId(context), message.StudentUserIds,
            "Ders iptal edildi", $"{TurkeyTime.Format(message.StartsAt)} tarihindeki ders iptal edildi.",
            "CourseManagement.LessonCancelled", message.LessonId.ToString(), context.CancellationToken));
    }
}

public sealed class CourseManagementAssignmentPublishedConsumer(ICourseManagementNotificationDispatcher dispatcher)
    : IConsumer<CourseManagementAssignmentPublishedV1>
{
    public Task Consume(ConsumeContext<CourseManagementAssignmentPublishedV1> context)
    {
        var message = context.Message;
        return CourseManagementConsumerMessageIds.DispatchAsync(context, () => dispatcher.SendAsync(
            CourseManagementConsumerMessageIds.RequireMessageId(context), message.StudentUserIds,
            $"Yeni ödev: {message.Title}", $"Son teslim: {TurkeyTime.Format(message.DueAt)}.",
            "CourseManagement.AssignmentPublished", message.AssignmentId.ToString(), context.CancellationToken));
    }
}

public sealed class CourseManagementAssignmentSubmittedConsumer(ICourseManagementNotificationDispatcher dispatcher)
    : IConsumer<CourseManagementAssignmentSubmittedV1>
{
    public Task Consume(ConsumeContext<CourseManagementAssignmentSubmittedV1> context)
    {
        var message = context.Message;
        return CourseManagementConsumerMessageIds.DispatchAsync(context, () => dispatcher.SendAsync(
            CourseManagementConsumerMessageIds.RequireMessageId(context), [message.TeacherUserId], "Ödev teslim edildi",
            "Bir öğrenci ödevini gönderdi. Teslimi kurs panelinden inceleyebilirsin.",
            "CourseManagement.AssignmentSubmitted", message.AssignmentId.ToString(), context.CancellationToken));
    }
}

public sealed class CourseManagementAssignmentReviewedConsumer(ICourseManagementNotificationDispatcher dispatcher)
    : IConsumer<CourseManagementAssignmentReviewedV1>
{
    public Task Consume(ConsumeContext<CourseManagementAssignmentReviewedV1> context)
    {
        var message = context.Message;
        return CourseManagementConsumerMessageIds.DispatchAsync(context, () => dispatcher.SendAsync(
            CourseManagementConsumerMessageIds.RequireMessageId(context), [message.StudentUserId], "Ödev değerlendirildi",
            "Puan ve geri bildirimi kurs panelinden görebilirsin.",
            "CourseManagement.AssignmentReviewed", message.AssignmentId.ToString(), context.CancellationToken));
    }
}

public sealed class CourseManagementPaymentRecordedConsumer(ICourseManagementNotificationDispatcher dispatcher)
    : IConsumer<CourseManagementPaymentRecordedV1>
{
    public Task Consume(ConsumeContext<CourseManagementPaymentRecordedV1> context)
    {
        var message = context.Message;
        return CourseManagementConsumerMessageIds.DispatchAsync(context, () => dispatcher.SendAsync(
            CourseManagementConsumerMessageIds.RequireMessageId(context), [message.StudentUserId], "Kurs finans özeti güncellendi",
            "Bakiye ve kalan paket hakların güncellendi.",
            "CourseManagement.PaymentRecorded", null, context.CancellationToken));
    }
}

public sealed class CourseManagementReminderDueConsumer(ICourseManagementNotificationDispatcher dispatcher)
    : IConsumer<CourseManagementReminderDueV1>
{
    public Task Consume(ConsumeContext<CourseManagementReminderDueV1> context)
    {
        var message = context.Message;
        var reminder = message.ReminderType switch
        {
            "Lesson" => ("Yaklaşan dersin", $"Dersin {TurkeyTime.Format(message.TargetAt)} tarihinde.",
                "CourseManagement.LessonReminder"),
            "Assignment" => ("Ödev teslim zamanı yaklaşıyor", $"Ödevinin son teslim zamanı {TurkeyTime.Format(message.TargetAt)}.",
                "CourseManagement.AssignmentReminder"),
            "InstallmentUpcoming" => ("Yaklaşan vade", "Kurs finans özetinde yaklaşan bir vade var. Güncel bakiyeni kurs panelinden görebilirsin.",
                "CourseManagement.InstallmentUpcomingReminder"),
            "InstallmentDue" => ("Bugünkü vade", "Kurs finans özetinde bugün bir vade bulunuyor. Güncel bakiyeni kurs panelinden görebilirsin.",
                "CourseManagement.InstallmentDueReminder"),
            _ => throw new InvalidOperationException($"Unsupported course-management reminder type: {message.ReminderType}.")
        };

        return CourseManagementConsumerMessageIds.DispatchAsync(context, () => dispatcher.SendAsync(
            CourseManagementConsumerMessageIds.RequireMessageId(context), [message.RecipientUserId],
            reminder.Item1, reminder.Item2, reminder.Item3, null, context.CancellationToken));
    }
}

file static class CourseManagementConsumerMessageIds
{
    public static Guid RequireMessageId<T>(ConsumeContext<T> context) where T : class
        => context.MessageId ?? throw new InvalidOperationException($"{typeof(T).Name}.MessageId is required.");

    public static async Task DispatchAsync<T>(ConsumeContext<T> context, Func<Task> dispatch) where T : class
    {
        await dispatch();
        CourseManagementEventDeliveryMetrics.RecordDeliveryDelay(context.SentTime);
    }
}
