using FluentValidation;

namespace Coaching.Application.Commands.CreateGoal;

public sealed class CreateGoalCommandValidator : AbstractValidator<CreateGoalCommand>
{
    public CreateGoalCommandValidator()
    {
        RuleFor(command => command.StudentId)
            .NotEmpty();

        RuleFor(command => command.Category)
            .IsInEnum();

        RuleFor(command => command.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.Description)
            .MaximumLength(2_000)
            .When(command => command.Description is not null);

        RuleFor(command => command.TargetDate)
            .GreaterThan(DateTime.UtcNow)
            .When(command => command.TargetDate.HasValue)
            .WithMessage("Target date must be in the future.");

        RuleFor(command => command.TargetScore)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(999.99m)
            .When(command => command.TargetScore.HasValue)
            .WithMessage("Target score cannot be negative.");
    }
}
