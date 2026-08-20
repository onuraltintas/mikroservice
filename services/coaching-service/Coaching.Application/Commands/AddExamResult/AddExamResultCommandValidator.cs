using FluentValidation;

namespace Coaching.Application.Commands.AddExamResult;

public sealed class AddExamResultCommandValidator : AbstractValidator<AddExamResultCommand>
{
    public AddExamResultCommandValidator()
    {
        RuleFor(command => command.ExamId)
            .NotEmpty();

        RuleFor(command => command.StudentId)
            .NotEmpty();

        RuleFor(command => command.Score)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Score cannot be negative");

        RuleFor(command => command.CorrectAnswers)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.WrongAnswers)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.EmptyAnswers)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.IdempotencyKey)
            .NotEmpty()
            .Matches("^[A-Za-z0-9._~-]{16,128}$")
            .WithMessage("Idempotency-Key 16-128 güvenli karakterden oluşmalıdır.");
    }
}
