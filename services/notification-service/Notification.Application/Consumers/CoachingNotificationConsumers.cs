using EduPlatform.Shared.Contracts.Events.Coaching;
using MassTransit;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public sealed class AssignmentCreatedConsumer : IConsumer<AssignmentCreatedEvent>
{
    private readonly ICoachingNotificationDispatcher _dispatcher;

    public AssignmentCreatedConsumer(ICoachingNotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task Consume(ConsumeContext<AssignmentCreatedEvent> context)
    {
        var message = context.Message;
        return _dispatcher.SendAsync(
            CoachingConsumerMessageIds.Require(context),
            message.StudentIds,
            $"Yeni ödev: {message.Title}",
            $"Ödevin son teslim tarihi: {message.DueDate:dd.MM.yyyy HH:mm}",
            "AssignmentCreated",
            message.AssignmentId.ToString(),
            context.CancellationToken);
    }
}

public sealed class AssignmentSubmittedConsumer : IConsumer<AssignmentSubmittedEvent>
{
    private readonly ICoachingNotificationDispatcher _dispatcher;

    public AssignmentSubmittedConsumer(ICoachingNotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task Consume(ConsumeContext<AssignmentSubmittedEvent> context)
    {
        var message = context.Message;
        if (!message.TeacherId.HasValue || message.TeacherId.Value == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        return _dispatcher.SendAsync(
            CoachingConsumerMessageIds.Require(context),
            new[] { message.TeacherId.Value },
            "Ödev teslim edildi",
            $"Bir öğrenci ödevini teslim etti: {message.SubmittedAt:dd.MM.yyyy HH:mm}",
            "AssignmentSubmitted",
            message.AssignmentId.ToString(),
            context.CancellationToken);
    }
}

public sealed class AssignmentUpdatedConsumer : IConsumer<AssignmentUpdatedEvent>
{
    private readonly ICoachingNotificationDispatcher _dispatcher;

    public AssignmentUpdatedConsumer(ICoachingNotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task Consume(ConsumeContext<AssignmentUpdatedEvent> context)
    {
        var message = context.Message;
        return _dispatcher.SendAsync(
            CoachingConsumerMessageIds.Require(context),
            message.StudentIds,
            "Ödev güncellendi",
            $"Ödevin yeni son teslim tarihi: {message.DueDate:dd.MM.yyyy HH:mm}",
            "AssignmentUpdated",
            message.AssignmentId.ToString(),
            context.CancellationToken);
    }
}

public sealed class AssignmentGradedConsumer : IConsumer<AssignmentGradedEvent>
{
    private readonly ICoachingNotificationDispatcher _dispatcher;

    public AssignmentGradedConsumer(ICoachingNotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task Consume(ConsumeContext<AssignmentGradedEvent> context)
    {
        var message = context.Message;
        var feedback = string.IsNullOrWhiteSpace(message.TeacherFeedback)
            ? string.Empty
            : $" Geri bildirim: {message.TeacherFeedback}";

        return _dispatcher.SendAsync(
            CoachingConsumerMessageIds.Require(context),
            new[] { message.StudentId },
            "Ödev değerlendirildi",
            $"Ödev puanın: {message.Score:0.##}.{feedback}",
            "AssignmentGraded",
            message.AssignmentId.ToString(),
            context.CancellationToken);
    }
}

public sealed class ExamResultAddedConsumer : IConsumer<ExamResultAddedEvent>
{
    private readonly ICoachingNotificationDispatcher _dispatcher;

    public ExamResultAddedConsumer(ICoachingNotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task Consume(ConsumeContext<ExamResultAddedEvent> context)
    {
        var message = context.Message;
        var ranking = message.Ranking.HasValue ? $" Sıralaman: {message.Ranking}." : string.Empty;

        return _dispatcher.SendAsync(
            CoachingConsumerMessageIds.Require(context),
            new[] { message.StudentId },
            "Sınav sonucu açıklandı",
            $"Sınav puanın: {message.Score:0.##}.{ranking}",
            "ExamResultAdded",
            message.ExamId.ToString(),
            context.CancellationToken);
    }
}

public sealed class ExamUpdatedConsumer : IConsumer<ExamUpdatedEvent>
{
    private readonly ICoachingNotificationDispatcher _dispatcher;

    public ExamUpdatedConsumer(ICoachingNotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task Consume(ConsumeContext<ExamUpdatedEvent> context)
    {
        var message = context.Message;
        return _dispatcher.SendAsync(
            CoachingConsumerMessageIds.Require(context),
            message.StudentIds,
            "Sınav bilgileri güncellendi",
            $"Sınav tarihi: {message.ExamDate:dd.MM.yyyy HH:mm}",
            "ExamUpdated",
            message.ExamId.ToString(),
            context.CancellationToken);
    }
}

public sealed class ExamResultUpdatedConsumer : IConsumer<ExamResultUpdatedEvent>
{
    private readonly ICoachingNotificationDispatcher _dispatcher;

    public ExamResultUpdatedConsumer(ICoachingNotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task Consume(ConsumeContext<ExamResultUpdatedEvent> context)
    {
        var message = context.Message;
        return _dispatcher.SendAsync(
            CoachingConsumerMessageIds.Require(context),
            new[] { message.StudentId },
            "Sınav sonucu düzeltildi",
            $"Güncel sınav puanın: {message.Score:0.##}.",
            "ExamResultUpdated",
            message.ExamId.ToString(),
            context.CancellationToken);
    }
}

public sealed class SessionScheduledConsumer : IConsumer<SessionScheduledEvent>
{
    private readonly ICoachingNotificationDispatcher _dispatcher;

    public SessionScheduledConsumer(ICoachingNotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task Consume(ConsumeContext<SessionScheduledEvent> context)
    {
        var message = context.Message;
        return _dispatcher.SendAsync(
            CoachingConsumerMessageIds.Require(context),
            message.StudentIds,
            "Yeni koçluk seansı",
            $"Koçluk seansın planlandı: {message.ScheduledDate:dd.MM.yyyy HH:mm}",
            "SessionScheduled",
            message.SessionId.ToString(),
            context.CancellationToken);
    }
}

public sealed class SessionUpdatedConsumer : IConsumer<SessionUpdatedEvent>
{
    private readonly ICoachingNotificationDispatcher _dispatcher;

    public SessionUpdatedConsumer(ICoachingNotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task Consume(ConsumeContext<SessionUpdatedEvent> context)
    {
        var message = context.Message;
        return _dispatcher.SendAsync(
            CoachingConsumerMessageIds.Require(context),
            message.StudentIds,
            "Koçluk seansı güncellendi",
            $"Güncel seans zamanı: {message.ScheduledDate:dd.MM.yyyy HH:mm}",
            "SessionUpdated",
            message.SessionId.ToString(),
            context.CancellationToken);
    }
}

public sealed class GoalCreatedConsumer : IConsumer<GoalCreatedEvent>
{
    private readonly ICoachingNotificationDispatcher _dispatcher;

    public GoalCreatedConsumer(ICoachingNotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task Consume(ConsumeContext<GoalCreatedEvent> context)
    {
        var message = context.Message;
        return _dispatcher.SendAsync(
            CoachingConsumerMessageIds.Require(context),
            new[] { message.StudentId },
            "Yeni akademik hedef",
            message.Title,
            "GoalCreated",
            message.GoalId.ToString(),
            context.CancellationToken);
    }
}

public sealed class GoalUpdatedConsumer : IConsumer<GoalUpdatedEvent>
{
    private readonly ICoachingNotificationDispatcher _dispatcher;

    public GoalUpdatedConsumer(ICoachingNotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task Consume(ConsumeContext<GoalUpdatedEvent> context)
    {
        var message = context.Message;
        return _dispatcher.SendAsync(
            CoachingConsumerMessageIds.Require(context),
            new[] { message.StudentId },
            "Akademik hedef güncellendi",
            message.Title,
            "GoalUpdated",
            message.GoalId.ToString(),
            context.CancellationToken);
    }
}

file static class CoachingConsumerMessageIds
{
    public static Guid Require<T>(ConsumeContext<T> context)
        where T : class
        => context.MessageId
           ?? throw new InvalidOperationException($"{typeof(T).Name}.MessageId is required.");
}
