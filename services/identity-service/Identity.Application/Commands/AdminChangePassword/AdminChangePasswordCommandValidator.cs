using FluentValidation;

namespace Identity.Application.Commands.AdminChangePassword;

public class AdminChangePasswordCommandValidator : AbstractValidator<AdminChangePasswordCommand>
{
    public AdminChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Kullanıcı ID boş olamaz.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Şifre boş olamaz.")
            .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalıdır.")
            .Matches(@"[A-Z]").WithMessage("Şifre en az bir büyük harf içermelidir.")
            .Matches(@"[a-z]").WithMessage("Şifre en az bir küçük harf içermelidir.")
            .Matches(@"[0-9]").WithMessage("Şifre en az bir rakam içermelidir.")
            .Matches(@"[!@#$%^&*(),.?""{}|<>]").WithMessage("Şifre en az bir özel karakter içermelidir.");
    }
}
