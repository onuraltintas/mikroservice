using FluentValidation;
using Coaching.Domain.Enums;

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

        RuleFor(x => x.AssignmentSource)
            .Must(BeValidAssignmentSource).WithMessage("Assignment source must be 'Digital', 'Book' or 'Mixed'");

        When(x => IsBookSource(x.AssignmentSource), () =>
        {
            RuleFor(x => x.BookTitle)
                .NotEmpty().WithMessage("Book title is required for book assignments")
                .MaximumLength(200);
            RuleFor(x => x.BookStartPage)
                .NotNull().GreaterThan(0).WithMessage("Book start page must be greater than 0");
            RuleFor(x => x.BookEndPage)
                .NotNull().GreaterThan(0).WithMessage("Book end page must be greater than 0")
                .GreaterThanOrEqualTo(x => x.BookStartPage)
                .WithMessage("Book end page cannot be before the start page");
            RuleFor(x => x.BookStartQuestion)
                .GreaterThan(0).When(x => x.BookStartQuestion.HasValue)
                .WithMessage("Book start question must be greater than 0");
            RuleFor(x => x.BookEndQuestion)
                .GreaterThanOrEqualTo(x => x.BookStartQuestion)
                .When(x => x.BookEndQuestion.HasValue)
                .WithMessage("Book end question cannot be before the start question");
            RuleFor(x => x)
                .Must(x => x.BookStartQuestion.HasValue == x.BookEndQuestion.HasValue)
                .WithMessage("Book question range must include both start and end");
        });

        When(x => !IsBookSource(x.AssignmentSource), () =>
        {
            RuleFor(x => x.BookTitle).Empty();
            RuleFor(x => x.BookStartPage).Null();
            RuleFor(x => x.BookEndPage).Null();
            RuleFor(x => x.BookStartQuestion).Null();
            RuleFor(x => x.BookEndQuestion).Null();
        });

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
            .NotEmpty().WithMessage("At least one student must be assigned")
            .Must(ids => ids.Count <= 100).WithMessage("At most 100 students may be assigned")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Student IDs must be unique");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .Matches("^[A-Za-z0-9._~-]{16,128}$")
            .WithMessage("Idempotency-Key 16-128 güvenli karakterden oluşmalıdır.");
    }

    private bool BeValidAssignmentType(string type)
    {
        return type.Equals("Individual", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("Group", StringComparison.OrdinalIgnoreCase);
    }

    private static bool BeValidAssignmentSource(string source) =>
        Enum.TryParse<AssignmentSource>(source, true, out var parsed)
        && Enum.IsDefined(parsed);

    private static bool IsBookSource(string source) =>
        BeValidAssignmentSource(source)
        && Enum.TryParse<AssignmentSource>(source, true, out var parsed)
        && parsed is AssignmentSource.Book or AssignmentSource.Mixed;
}
