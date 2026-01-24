using FluentValidation;

namespace Coaching.Application.Commands.CreateSession;

public class CreateSessionCommandValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionCommandValidator()
    {
        RuleFor(x => x.TeacherId).NotEmpty();
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.StartTime).GreaterThan(DateTime.UtcNow).WithMessage("Session start time must be in the future");
        RuleFor(x => x.DurationMinutes).GreaterThan(0).LessThanOrEqualTo(240); // Max 4 hours
        RuleFor(x => x.Subject).MaximumLength(200);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
