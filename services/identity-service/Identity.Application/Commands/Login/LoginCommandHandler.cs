using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Microsoft.Extensions.Logging.ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        Microsoft.Extensions.Logging.ILogger<LoginCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // 1. Get User
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user == null)
        {
            return Result.Failure<LoginResponse>(new Error("Auth.InvalidCredentials", "E-posta veya şifre hatalı."));
        }

        // 2. Verify Password
        if (!_passwordHasher.VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return Result.Failure<LoginResponse>(new Error("Auth.InvalidCredentials", "E-posta veya şifre hatalı."));
        }

        // 3. Check Active Status
        if (!user.IsActive)
        {
             return Result.Failure<LoginResponse>(new Error("Auth.UserInactive", "Hesabınız pasif durumdadır."));
        }

        // 4. Check Email Confirmation
        var isAdmin = user.Roles.Any(r => 
            r.Role.Name == "SystemAdmin" || 
            r.Role.Name == "InstitutionAdmin" || 
            r.Role.Name == "InstitutionOwner");

        if (!user.EmailConfirmed && !isAdmin)
        {
            return Result.Failure<LoginResponse>(new Error("Auth.EmailNotConfirmed", "Lütfen e-posta adresinizi doğrulayın."));
        }

        // 5. Generate Tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        
        var refreshToken = _tokenService.GenerateRefreshToken(user.Id, "0.0.0.0"); 
        
        try 
        {
            user.AddRefreshToken(refreshToken);
            user.RecordLogin();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
             // Log but don't fail login if DB recording fails in dev
             _logger.LogError(ex, "Login recording failed");
        }

        return Result.Success(new LoginResponse(accessToken, refreshToken.Token));
    }
}
