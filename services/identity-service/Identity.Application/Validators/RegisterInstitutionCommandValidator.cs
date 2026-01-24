using FluentValidation;
using Identity.Application.Commands.RegisterInstitution;

namespace Identity.Application.Validators;

public class RegisterInstitutionCommandValidator : AbstractValidator<RegisterInstitutionCommand>
{
    public RegisterInstitutionCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required")
            .EmailAddress().WithMessage("A valid email address is required");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character");

        RuleFor(x => x.InstitutionName)
            .NotEmpty().WithMessage("Institution name is required")
            .MaximumLength(200).WithMessage("Institution name cannot exceed 200 characters");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required");
            
        RuleFor(x => x.City)
            .NotEmpty();
    }
}
