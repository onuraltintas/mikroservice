using FluentValidation;

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
            .Must(role => !string.IsNullOrWhiteSpace(role))
            .WithMessage("Rol seçimi zorunludur.")
            .MaximumLength(50).WithMessage("Rol en fazla 50 karakter olabilir.");

        RuleFor(x => x)
            .Must(request =>
            {
                var products = request.Products ?? (request.Product is { } product ? [product] : []);
                return products.Length > 0 && products.All(product => product is Identity.Domain.Enums.PlatformProduct.Coaching or Identity.Domain.Enums.PlatformProduct.SpeedReading);
            })
            .When(x => !string.Equals(x.Role?.Trim(), "SystemAdmin", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Koçluk veya Hızlı Okuma servisi seçilmelidir.");

        // Telefon numarası opsiyoneldir, ancak girildiyse formatı kontrol edilir.
        // Basit Regex: + ile başlayabilir, en az 8 hane.
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[0-9]{8,15}$")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber))
            .WithMessage("Geçerli bir telefon numarası giriniz.");
    }
}
