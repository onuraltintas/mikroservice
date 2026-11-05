using FluentValidation;
using Identity.Application.Validators;

namespace Identity.Application.Commands.RegisterTeacher;

public sealed class RegisterTeacherCommandValidator : AbstractValidator<RegisterTeacherCommand>
{
    public RegisterTeacherCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta alanı zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.")
            .MaximumLength(255).WithMessage("E-posta adresi en fazla 255 karakter olabilir.");
        RuleFor(x => x.Password).ApplyRegistrationPasswordPolicy();
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad alanı zorunludur.")
            .MaximumLength(100).WithMessage("Ad en fazla 100 karakter olabilir.");
        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad alanı zorunludur.")
            .MaximumLength(100).WithMessage("Soyad en fazla 100 karakter olabilir.");
        RuleFor(x => x.Phone)
            .Matches(@"^\+?[0-9]{8,15}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Geçerli bir telefon numarası giriniz.");
    }
}
