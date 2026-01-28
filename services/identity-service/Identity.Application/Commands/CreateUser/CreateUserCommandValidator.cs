using FluentValidation;
using Identity.Domain.Enums;

namespace Identity.Application.Commands.CreateUser;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad alanı boş bırakılamaz.")
            .MaximumLength(50).WithMessage("Ad alanı en fazla 50 karakter olabilir.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad alanı boş bırakılamaz.")
            .MaximumLength(50).WithMessage("Soyad alanı en fazla 50 karakter olabilir.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta alanı boş bırakılamaz.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Rol seçimi zorunludur.")
            .Must(BeAValidRole).WithMessage("'{PropertyValue}' geçerli bir rol değil. (Beklenen: Student, Teacher, Parent, InstitutionAdmin)");
            
        // Telefon numarası opsiyoneldir, ancak girildiyse formatı kontrol edilir.
        // Basit Regex: + ile başlayabilir, en az 8 hane.
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[0-9]{8,15}$")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber))
            .WithMessage("Geçerli bir telefon numarası giriniz.");
    }

    private bool BeAValidRole(string role)
    {
        return Enum.IsDefined(typeof(UserRole), role) 
            || Enum.GetNames<UserRole>().Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
