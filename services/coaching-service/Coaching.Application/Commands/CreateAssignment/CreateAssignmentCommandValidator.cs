using FluentValidation;

namespace Coaching.Application.Commands.CreateAssignment;

/// <summary>
/// CreateAssignmentCommand Validator
/// </summary>
public class CreateAssignmentCommandValidator : AbstractValidator<CreateAssignmentCommand>
{
    public CreateAssignmentCommandValidator()
    {
        RuleFor(x => x.TeacherId)
            .NotEmpty().WithMessage("Teacher ID is required");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters");

        RuleFor(x => x.DueDate)
            .GreaterThan(DateTime.UtcNow).WithMessage("Due date must be in the future");

        RuleFor(x => x.AssignmentType)
            .Must(BeValidAssignmentType).WithMessage("Assignment type must be 'Individual' or 'Group'");

        RuleFor(x => x.TargetGradeLevel)
            .InclusiveBetween(1, 12).When(x => x.TargetGradeLevel.HasValue)
            .WithMessage("Grade level must be between 1 and 12");

        RuleFor(x => x.MaxScore)
            .GreaterThan(0).When(x => x.MaxScore.HasValue)
            .WithMessage("Max score must be greater than 0");

        RuleFor(x => x.PassingScore)
            .LessThanOrEqualTo(x => x.MaxScore).When(x => x.PassingScore.HasValue && x.MaxScore.HasValue)
            .WithMessage("Passing score cannot exceed max score");

        RuleFor(x => x.StudentIds)
            .NotEmpty().WithMessage("At least one student must be assigned");
    }

    private bool BeValidAssignmentType(string type)
    {
        return type.Equals("Individual", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("Group", StringComparison.OrdinalIgnoreCase);
    }
}
