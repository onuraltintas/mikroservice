using FluentValidation;
using Identity.Application.Validators;

namespace Identity.Application.Commands.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        // The current password is only verified against the stored hash: accounts created under an
        // older, shorter rule must still be able to change it.
        RuleFor(x => x.CurrentPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Mevcut şifre boş olamaz.")
            .MaximumLength(RegistrationPasswordRules.MaximumLength)
            .WithMessage("Mevcut şifre en fazla 128 karakter olabilir.");

        RuleFor(x => x.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Yeni şifre boş olamaz.")
            .ApplyRegistrationPasswordPolicy();
    }
}
