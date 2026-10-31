using Notification.Application.Configuration;
using EduPlatform.Shared.Contracts.Events.Coaching;
using MassTransit;
using Notification.Application.Messaging;
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
            MessageIds.Require(context),
            message.StudentIds,
            $"Yeni ödev: {message.Title}",
            $"\"{message.Title}\" ödevinin son teslim tarihi: {TurkeyTime.Format(message.DueDate)}",
            "AssignmentCreated",
            message.AssignmentId.ToString(),
            context.CancellationToken);
    }
}

public sealed class AssignmentSubmittedConsumer : IConsumer<AssignmentSubmittedEvent>
{
    private readonly ICoachingNotificationDispatcher _dispatcher;
    private readonly IRecipientDirectory? _directory;

    public AssignmentSubmittedConsumer(ICoachingNotificationDispatcher dispatcher, IRecipientDirectory? directory = null)
    {
        _dispatcher = dispatcher;
        _directory = directory;
    }

    public async Task Consume(ConsumeContext<AssignmentSubmittedEvent> context)
    {
        var message = context.Message;
        if (!message.TeacherId.HasValue || message.TeacherId.Value == Guid.Empty)
        {
            return;
        }

        // Roadmap O8: the teacher sees who handed in which assignment.
        var student = _directory is null ? null
            : (await _directory.GetAsync([message.StudentId], context.CancellationToken)).GetValueOrDefault(message.StudentId)?.DisplayName;
        var text = Notification.Application.Services.CoachingNotificationTexts.AssignmentSubmitted(student, message.AssignmentTitle, message.SubmittedAt);
        await _dispatcher.SendAsync(
            MessageIds.Require(context),
            new[] { message.TeacherId.Value },
            text.Title,
            text.Message,
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
            MessageIds.Require(context),
            message.StudentIds,
            $"Ödev güncellendi: {message.Title}",
            $"\"{message.Title}\" ödevinin yeni son teslim tarihi: {TurkeyTime.Format(message.DueDate)}",
            "AssignmentUpdated",
            message.AssignmentId.ToString(),
            context.CancellationToken);
    }
}

public sealed class AssignmentCancelledConsumer(ICoachingNotificationDispatcher dispatcher) : IConsumer<AssignmentCancelledEvent>
{
    public Task Consume(ConsumeContext<AssignmentCancelledEvent> context)
    {
        var message = context.Message;
        return dispatcher.SendAsync(
            MessageIds.Require(context),
            message.StudentIds,
            "Ödev iptal edildi",
            $"\"{message.Title}\" ödevi öğretmenin tarafından iptal edildi. Bu ödev için teslim yapman gerekmiyor.",
            "AssignmentCancelled",
            message.AssignmentId.ToString(),
            context.CancellationToken);
    }
}

public sealed class CoachingTeacherLinkEndedConsumer(ICoachingNotificationDispatcher dispatcher) : IConsumer<CoachingTeacherLinkEndedEvent>
{
    public async Task Consume(ConsumeContext<CoachingTeacherLinkEndedEvent> context)
    {
        var message = context.Message;
        // K2 (31.10.2026): the institution ended the link; the student, the teacher and the institution admin are told what closed.
        if (message.Reason == CoachingTeacherLinkEndedReasons.InstitutionEnded)
        {
            var id = MessageIds.Require(context);
            var teacher = string.IsNullOrWhiteSpace(message.TeacherName) ? "Öğretmenin" : message.TeacherName;
            await dispatcher.SendAsync(id, new[] { message.StudentUserId }, "Öğretmen bağlantısı sona erdi",
                $"Kurumun, {teacher} ile koçluk bağlantını sonlandırdı.{Closed(message, true)} Geçmiş kayıtların korunuyor.",
                "TeacherLinkEnded", message.TeacherUserId.ToString(), context.CancellationToken);
            await dispatcher.SendAsync(id, new[] { message.TeacherUserId }, "Öğrenci bağlantısı sona erdi",
                $"Kurum, {message.StudentName} ile koçluk bağlantınızı sonlandırdı.{Closed(message, false)}",
                "TeacherLinkEnded", message.StudentUserId.ToString(), context.CancellationToken);
            if (message.InstitutionActorUserId is { } admin && admin != Guid.Empty)
                await dispatcher.SendAsync(id, new[] { admin }, "Koçluk bağlantısı sonlandırıldı",
                    $"{message.StudentName} ile {(string.IsNullOrWhiteSpace(message.TeacherName) ? "öğretmeni" : message.TeacherName)} arasındaki bağlantı sonlandırıldı.{Closed(message, false)}",
                    "TeacherLinkEnded", message.StudentUserId.ToString(), context.CancellationToken);
            return;
        }
        // Roadmap O12: when the teacher ends the link, the student is the one to tell.
        if (message.Reason == CoachingTeacherLinkEndedReasons.TeacherRemoved)
        {
            await dispatcher.SendAsync(
                MessageIds.Require(context),
                new[] { message.StudentUserId },
                "Öğretmen bağlantısı sona erdi",
                $"{(string.IsNullOrWhiteSpace(message.TeacherName) ? "Öğretmenin" : message.TeacherName)} koçluk bağlantını sonlandırdı. Geçmiş kayıtların korunuyor.",
                "TeacherLinkEnded",
                message.TeacherUserId.ToString(),
                context.CancellationToken);
            return;
        }
        var text = message.Reason == CoachingTeacherLinkEndedReasons.JoinedInstitution
            ? $"{message.StudentName} bir kuruma katıldı; bireysel koçluk bağlantınız sona erdi."
            : $"{message.StudentName} koçluk bağlantınızı sonlandırdı.";
        text += Closed(message, false);
        await dispatcher.SendAsync(
            MessageIds.Require(context),
            new[] { message.TeacherUserId },
            "Öğrenci bağlantısı sona erdi",
            text,
            "TeacherLinkEnded",
            message.StudentUserId.ToString(),
            context.CancellationToken);
    }

    /// <summary>What closed with the link: open assignments withdrawn and coming sessions left.</summary>
    private static string Closed(CoachingTeacherLinkEndedEvent message, bool forStudent)
    {
        var parts = new List<string>();
        if (message.CancelledAssignments > 0) parts.Add($"{message.CancelledAssignments} ödev iptal edildi");
        if (message.RemovedSessions > 0)
            parts.Add(forStudent ? $"{message.RemovedSessions} seanstan çıkarıldın" : $"{message.RemovedSessions} seans kapatıldı");
        return parts.Count == 0 ? string.Empty : $" {string.Join(", ", parts)}.";
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
        var text = Notification.Application.Services.CoachingNotificationTexts.AssignmentGraded(message.AssignmentTitle, message.Score, message.TeacherFeedback);

        return _dispatcher.SendAsync(
            MessageIds.Require(context),
            new[] { message.StudentId },
            text.Title,
            text.Message,
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
            MessageIds.Require(context),
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
            MessageIds.Require(context),
            message.StudentIds,
            "Sınav bilgileri güncellendi",
            $"Sınav tarihi: {TurkeyTime.Format(message.ExamDate)}",
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
            MessageIds.Require(context),
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
            MessageIds.Require(context),
            message.StudentIds,
            "Yeni koçluk seansı",
            $"Koçluk seansın planlandı: {TurkeyTime.Format(message.ScheduledDate)}",
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
            MessageIds.Require(context),
            message.StudentIds,
            "Koçluk seansı güncellendi",
            $"Güncel seans zamanı: {TurkeyTime.Format(message.ScheduledDate)}",
            "SessionUpdated",
            message.SessionId.ToString(),
            context.CancellationToken);
    }
}

public sealed class SessionCancelledConsumer : IConsumer<SessionCancelledEvent>
{
    private readonly ICoachingNotificationDispatcher _dispatcher;

    public SessionCancelledConsumer(ICoachingNotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task Consume(ConsumeContext<SessionCancelledEvent> context)
    {
        var message = context.Message;
        return _dispatcher.SendAsync(
            MessageIds.Require(context),
            message.StudentIds,
            "Koçluk seansı iptal edildi",
            $"Koçluk seansın iptal edildi: {TurkeyTime.Format(message.ScheduledDate)}",
            "SessionCancelled",
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
            MessageIds.Require(context),
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
            MessageIds.Require(context),
            new[] { message.StudentId },
            "Akademik hedef güncellendi",
            message.Title,
            "GoalUpdated",
            message.GoalId.ToString(),
            context.CancellationToken);
    }
}

/// <summary>Roadmap O3: tells the recipient who wrote, never what was written.</summary>
public sealed class CoachingMessageReceivedConsumer(ICoachingNotificationDispatcher dispatcher) : IConsumer<CoachingMessageReceivedEvent>
{
    public Task Consume(ConsumeContext<CoachingMessageReceivedEvent> context)
    {
        var message = context.Message;
        var sender = string.IsNullOrWhiteSpace(message.SenderName) ? "Koçluk" : message.SenderName.Trim();
        return dispatcher.SendAsync(
            MessageIds.Require(context),
            new[] { message.RecipientUserId },
            $"Yeni mesaj: {sender}",
            message.HasAttachments ? $"{sender} sana dosya ekli bir mesaj gönderdi." : $"{sender} sana mesaj gönderdi.",
            "CoachingMessage",
            message.ConversationId.ToString(),
            context.CancellationToken);
    }
}

/// <summary>Roadmap O8: the inviter learns that the invitation was accepted and who joined.</summary>
public sealed class InvitationAcceptedConsumer(ICoachingNotificationDispatcher dispatcher)
    : IConsumer<EduPlatform.Shared.Contracts.Events.Identity.InvitationAcceptedEvent>
{
    public Task Consume(ConsumeContext<EduPlatform.Shared.Contracts.Events.Identity.InvitationAcceptedEvent> context)
    {
        var message = context.Message;
        if (message.InviterId == Guid.Empty || message.InviterId == message.InviteeId) return Task.CompletedTask;
        var text = Notification.Application.Services.CoachingNotificationTexts.InvitationAccepted(message.InviteeName, message.InvitationType);
        return dispatcher.SendAsync(MessageIds.Require(context), [message.InviterId], text.Title, text.Message,
            "InvitationAccepted", message.InvitationId.ToString(), context.CancellationToken);
    }
}
