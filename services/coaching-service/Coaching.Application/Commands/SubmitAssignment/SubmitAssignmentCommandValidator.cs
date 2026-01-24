using FluentValidation;

namespace Coaching.Application.Commands.SubmitAssignment;

public class SubmitAssignmentCommandValidator : AbstractValidator<SubmitAssignmentCommand>
{
    public SubmitAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId)
            .NotEmpty().WithMessage("Assignment ID is required");

        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("Student ID is required");

        RuleFor(x => x.StudentNote)
            .MaximumLength(1000).When(x => !string.IsNullOrEmpty(x.StudentNote))
            .WithMessage("Student note cannot exceed 1000 characters");
    }
}
