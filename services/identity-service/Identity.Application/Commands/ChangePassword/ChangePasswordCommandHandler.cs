using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.ChangePassword;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public ChangePasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId == null)
        {
            return Result.Failure(new Error("Auth.Unauthorized", "Oturum açılmamış."));
        }

        var user = await _userRepository.GetByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        if (user == null)
        {
            return Result.Failure(new Error("User.NotFound", "Kullanıcı bulunamadı."));
        }

        // Verify current password
        if (!_passwordHasher.VerifyPasswordHash(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
        {
            return Result.Failure(new Error("Auth.InvalidCredentials", "Mevcut şifre hatalı."));
        }

        // Create new hash
        _passwordHasher.CreatePasswordHash(request.NewPassword, out byte[] newHash, out byte[] newSalt);
        
        user.SetPassword(newHash, newSalt);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
