using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public ResetPasswordCommandHandler(
        IUserRepository userRepository, 
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user == null)
        {
            return Result.Failure(new Error("ResetPassword.UserNotFound", "Kullanıcı bulunamadı."));
        }

        if (user.PasswordResetToken == null || user.PasswordResetToken != request.Token)
        {
            return Result.Failure(new Error("ResetPassword.InvalidToken", "Geçersiz sıfırlama kodu."));
        }

        if (user.PasswordResetTokenExpiresAt < DateTime.UtcNow)
        {
            return Result.Failure(new Error("ResetPassword.ExpiredToken", "Sıfırlama kodunun süresi dolmuş."));
        }

        // Validate password strength
        var password = request.NewPassword;
        if (password.Length < 8)
        {
            return Result.Failure(new Error("ResetPassword.WeakPassword", "Şifre en az 8 karakter olmalıdır."));
        }
        if (!password.Any(char.IsUpper))
        {
            return Result.Failure(new Error("ResetPassword.WeakPassword", "Şifre en az bir büyük harf içermelidir."));
        }
        if (!password.Any(char.IsLower))
        {
            return Result.Failure(new Error("ResetPassword.WeakPassword", "Şifre en az bir küçük harf içermelidir."));
        }
        if (!password.Any(char.IsDigit))
        {
            return Result.Failure(new Error("ResetPassword.WeakPassword", "Şifre en az bir rakam içermelidir."));
        }
        if (!password.Any(c => "!@#$%^&*(),.?\":{}|<>_-+=[]\\;'/`~".Contains(c)))
        {
            return Result.Failure(new Error("ResetPassword.WeakPassword", "Şifre en az bir özel karakter içermelidir."));
        }

        // Reset password
        _passwordHasher.CreatePasswordHash(request.NewPassword, out byte[] passwordHash, out byte[] passwordSalt);
        user.SetPassword(passwordHash, passwordSalt);
        
        // Clear token
        user.ClearPasswordResetToken();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
