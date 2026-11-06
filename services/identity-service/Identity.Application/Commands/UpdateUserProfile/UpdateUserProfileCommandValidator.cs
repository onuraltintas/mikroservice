using FluentValidation;

namespace Identity.Application.Commands.UpdateUserProfile;

public class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator()
    {
        // E5 (#22): Hesabım edits the name; a name that is sent cannot be blank.
        RuleFor(x => x.FirstName)
            .Cascade(CascadeMode.Stop)
            .Must(value => value is null || !string.IsNullOrWhiteSpace(value)).WithMessage("Ad boş olamaz.")
            .MaximumLength(100).WithMessage("Ad en fazla 100 karakter olabilir.");

        RuleFor(x => x.LastName)
            .Cascade(CascadeMode.Stop)
            .Must(value => value is null || !string.IsNullOrWhiteSpace(value)).WithMessage("Soyad boş olamaz.")
            .MaximumLength(100).WithMessage("Soyad en fazla 100 karakter olabilir.");

        RuleFor(x => x.Bio)
            .MaximumLength(500).WithMessage("Hakkımda en fazla 500 karakter olabilir.");
            
        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).WithMessage("Telefon numarası en fazla 20 karakter olabilir.");

        RuleFor(x => x.GradeLevel)
            .InclusiveBetween(1, 12).When(x => x.GradeLevel.HasValue)
            .WithMessage("Sınıf 1 ile 12 arasında olmalıdır.");
    }
}
