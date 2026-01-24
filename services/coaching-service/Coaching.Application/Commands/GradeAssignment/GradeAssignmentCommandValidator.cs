using FluentValidation;

namespace Coaching.Application.Commands.GradeAssignment;

public class GradeAssignmentCommandValidator : AbstractValidator<GradeAssignmentCommand>
{
    public GradeAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId)
            .NotEmpty().WithMessage("Assignment ID is required");

        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("Student ID is required");

        RuleFor(x => x.Score)
            .GreaterThanOrEqualTo(0).WithMessage("Score cannot be negative");

        RuleFor(x => x.TeacherFeedback)
            .MaximumLength(2000).When(x => !string.IsNullOrEmpty(x.TeacherFeedback))
            .WithMessage("Teacher feedback cannot exceed 2000 characters");
    }
}
