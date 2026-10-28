using FluentValidation;
using Identity.Application.Validators;

namespace Identity.Application.Commands.RegisterStudent;

public class RegisterStudentCommandValidator : AbstractValidator<RegisterStudentCommand>
{
    public RegisterStudentCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad alanı zorunludur.")
            .MaximumLength(50);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad alanı zorunludur.")
            .MaximumLength(50);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta alanı zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.Password).ApplyRegistrationPasswordPolicy();

        // Roadmap Y8: Coaching asks for the birth date (no parental consent flow yet, only the date).
        RuleFor(x => x.BirthDate)
            .NotNull().WithMessage("Doğum tarihi zorunludur.")
            .Must(BePlausibleBirthDate).WithMessage("Geçerli bir doğum tarihi giriniz.")
            .When(x => x.Product == Identity.Domain.Enums.PlatformProduct.Coaching);

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
