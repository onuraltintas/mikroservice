using FluentValidation;
using Identity.Application.Commands.RegisterInstitution;

namespace Identity.Application.Validators;

public class RegisterInstitutionCommandValidator : AbstractValidator<RegisterInstitutionCommand>
{
    public RegisterInstitutionCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta alanı zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.Password).ApplyRegistrationPasswordPolicy();

        RuleFor(x => x.InstitutionName)
            .NotEmpty().WithMessage("Kurum adı zorunludur.")
            .MaximumLength(200).WithMessage("Kurum adı en fazla 200 karakter olabilir.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad alanı zorunludur.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad alanı zorunludur.");
            
        RuleFor(x => x.ProvinceId)
            .NotEmpty().WithMessage("İl seçimi zorunludur.")
            .MaximumLength(12).WithMessage("Geçerli bir il seçiniz.");

        RuleFor(x => x.DistrictId)
            .NotEmpty().WithMessage("İlçe seçimi zorunludur.")
            .MaximumLength(16).WithMessage("Geçerli bir ilçe seçiniz.");
    }
}
