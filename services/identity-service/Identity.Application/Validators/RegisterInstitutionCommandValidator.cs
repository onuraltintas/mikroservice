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

        RuleFor(x => x.Password).ApplyRegistrationPasswordPolicy();

        RuleFor(x => x.InstitutionName)
            .NotEmpty().WithMessage("Institution name is required")
            .MaximumLength(200).WithMessage("Institution name cannot exceed 200 characters");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required");
            
        RuleFor(x => x.ProvinceId)
            .NotEmpty()
            .MaximumLength(12);

        RuleFor(x => x.DistrictId)
            .NotEmpty()
            .MaximumLength(16);
    }
}
