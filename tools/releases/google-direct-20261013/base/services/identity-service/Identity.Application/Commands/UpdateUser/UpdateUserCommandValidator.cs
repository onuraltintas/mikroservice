using FluentValidation;

namespace Identity.Application.Commands.UpdateUser;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Kullanıcı ID boş olamaz.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad alanı boş bırakılamaz.")
            .MaximumLength(50).WithMessage("Ad alanı en fazla 50 karakter olabilir.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad alanı boş bırakılamaz.")
            .MaximumLength(50).WithMessage("Soyad alanı en fazla 50 karakter olabilir.");

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[0-9]{8,15}$")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber))
            .WithMessage("Geçerli bir telefon numarası giriniz.");

        RuleFor(x => x.Bio)
            .MaximumLength(500)
            .When(x => x.Bio is not null)
            .WithMessage("Biyografi en fazla 500 karakter olabilir.");

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(1000)
            .When(x => x.AvatarUrl is not null)
            .WithMessage("Avatar adresi en fazla 1000 karakter olabilir.");

        RuleFor(x => x.TeacherExperienceYears)
            .InclusiveBetween(0, 80)
            .When(x => x.TeacherExperienceYears.HasValue)
            .WithMessage("Deneyim yılı 0 ile 80 arasında olmalıdır.");

        RuleForEach(x => x.TeacherSubjects)
            .NotEmpty()
            .MaximumLength(100)
            .When(x => x.TeacherSubjects is not null)
            .WithMessage("Uzmanlık alanları boş olamaz ve 100 karakteri geçemez.");

        RuleFor(x => x.TeacherSubjects)
            .Must(subjects => subjects is null || subjects.Length <= 20)
            .WithMessage("En fazla 20 uzmanlık alanı eklenebilir.");

        RuleFor(x => x.StudentGradeLevel)
            .InclusiveBetween(1, 12)
            .When(x => x.StudentGradeLevel.HasValue)
            .WithMessage("Sınıf seviyesi 1 ile 12 arasında olmalıdır.");
    }
}
