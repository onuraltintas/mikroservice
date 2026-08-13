using EduPlatform.Shared.Kernel.Results;
using FluentValidation;
using MediatR;

namespace Notification.Application.Commands.SubmitSupportRequest;

public record SubmitSupportRequestCommand(
    string FirstName,
    string LastName,
    string Email,
    string Subject,
    string Message) : IRequest<Result<Guid>>;

public sealed class SubmitSupportRequestCommandValidator : AbstractValidator<SubmitSupportRequestCommand>
{
    public SubmitSupportRequestCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().MinimumLength(10).MaximumLength(2000);
    }
}
