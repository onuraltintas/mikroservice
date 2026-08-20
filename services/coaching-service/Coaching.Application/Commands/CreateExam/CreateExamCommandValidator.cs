using FluentValidation;

namespace Coaching.Application.Commands.CreateExam;

public sealed class CreateExamCommandValidator : AbstractValidator<CreateExamCommand>
{
    public CreateExamCommandValidator()
    {
        RuleFor(command => command.TeacherId)
            .NotEmpty();

        RuleFor(command => command.Type)
            .IsInEnum();

        RuleFor(command => command.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.ExamDate)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Exam date must be in the future.");

        RuleFor(command => command.MaxScore)
            .GreaterThan(0)
            .LessThanOrEqualTo(999.99m)
            .WithMessage("Max score must be greater than 0.");

        RuleFor(command => command.Description)
            .MaximumLength(2_000)
            .When(command => command.Description is not null);

        RuleFor(command => command.IdempotencyKey)
            .NotEmpty()
            .Matches("^[A-Za-z0-9._~-]{16,128}$")
            .WithMessage("Idempotency-Key 16-128 güvenli karakterden oluşmalıdır.");
    }
}
