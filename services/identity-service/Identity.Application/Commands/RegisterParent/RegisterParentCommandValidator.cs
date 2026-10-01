using FluentValidation;
using Identity.Application.Validators;

namespace Identity.Application.Commands.RegisterParent;

public sealed class RegisterParentCommandValidator : AbstractValidator<RegisterParentCommand>
{
    public RegisterParentCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Password).ApplyRegistrationPasswordPolicy();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[0-9]{8,15}$")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
    }
}
