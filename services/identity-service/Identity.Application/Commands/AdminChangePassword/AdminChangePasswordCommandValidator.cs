using FluentValidation;
using Identity.Application.Validators;

namespace Identity.Application.Commands.AdminChangePassword;

public class AdminChangePasswordCommandValidator : AbstractValidator<AdminChangePasswordCommand>
{
    public AdminChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Kullanıcı ID boş olamaz.");

        // Same policy as registration, self change and reset (RegistrationPasswordRules).
        RuleFor(x => x.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Şifre boş olamaz.")
            .ApplyRegistrationPasswordPolicy();
    }
}
