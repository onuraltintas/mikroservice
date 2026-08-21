using FluentValidation;
using MediatR;

namespace Coaching.Application.Commands.UpdateGoalProgress;

public record UpdateGoalProgressCommand(Guid GoalId, int Progress) : IRequest;

public sealed class UpdateGoalProgressCommandValidator : AbstractValidator<UpdateGoalProgressCommand>
{
    public UpdateGoalProgressCommandValidator()
    {
        RuleFor(command => command.GoalId)
            .NotEmpty();

        RuleFor(command => command.Progress)
            .InclusiveBetween(0, 100)
            .WithMessage("Progress must be between 0 and 100.");
    }
}
