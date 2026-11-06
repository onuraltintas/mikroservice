using FluentValidation;
using Identity.Application.Validators;

namespace Identity.Application.Commands.RegisterStudent;

public class RegisterStudentCommandValidator : AbstractValidator<RegisterStudentCommand>
{
    public RegisterStudentCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad alanı zorunludur.")
            .MaximumLength(50).WithMessage("Ad en fazla 50 karakter olabilir.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad alanı zorunludur.")
            .MaximumLength(50).WithMessage("Soyad en fazla 50 karakter olabilir.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta alanı zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.Password).ApplyRegistrationPasswordPolicy();

        // Paket A (05.11.2026): registration no longer asks for the birth date (the first-login
        // wizard will). Older clients may still send one; it must then be a plausible date.
        RuleFor(x => x.BirthDate)
            .Must(BePlausibleBirthDate).WithMessage("Geçerli bir doğum tarihi giriniz.")
            .When(x => x.BirthDate.HasValue);

        // Telefon validasyonu (opsiyonel)
        RuleFor(x => x.Phone)
            .Matches(@"^\+?[0-9]{10,15}$")
            .When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("Geçerli bir telefon numarası giriniz.");
    }

    private static bool BePlausibleBirthDate(DateOnly? birthDate)
    {
        if (birthDate is not { } date) return true;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return date <= today.AddYears(-3) && date >= today.AddYears(-100);
    }
}
