using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Application.Validators;
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

        if (!RegistrationPasswordRules.IsValid(request.NewPassword))
        {
            return Result.Failure(new Error(
                "ResetPassword.WeakPassword",
                "Şifre 8-128 karakter arasında olmalı; büyük harf, küçük harf, rakam ve özel karakter içermelidir."));
        }

        // Reset password
        _passwordHasher.CreatePasswordHash(request.NewPassword, out byte[] passwordHash, out byte[] passwordSalt);
        user.SetPassword(passwordHash, passwordSalt);
        
        // Clear token
        user.ClearPasswordResetToken();

        await _userRepository.RevokeActiveRefreshTokensAsync(
            user.Id,
            "security-sensitive password reset",
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
