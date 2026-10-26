using FluentValidation;

namespace Notification.Application.Commands.ReplyToSupportRequest;

public sealed class ReplyToSupportRequestCommandValidator : AbstractValidator<ReplyToSupportRequestCommand>
{
    public const int MaxReplyLength = 5000;

    public ReplyToSupportRequestCommandValidator()
    {
        RuleFor(command => command.SupportRequestId).NotEmpty();
        RuleFor(command => command.ReplyMessage)
            .NotEmpty().WithMessage("Yanıt metni zorunludur.")
            .MaximumLength(MaxReplyLength).WithMessage($"Yanıt en fazla {MaxReplyLength} karakter olabilir.");
    }
}
