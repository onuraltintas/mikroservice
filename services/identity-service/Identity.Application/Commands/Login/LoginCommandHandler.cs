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
    private readonly IIdentityService _identityService;
    private readonly IConfigurationService _configurationService;
    private readonly Microsoft.Extensions.Logging.ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        IIdentityService identityService,
        IConfigurationService configurationService,
        Microsoft.Extensions.Logging.ILogger<LoginCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _identityService = identityService;
        _configurationService = configurationService;
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

        // 4. Check Email Confirmation & IsAdmin Check
        var isAdmin = user.Roles.Any(r => 
            r.Role.Name == "SystemAdmin" || 
            r.Role.Name == "InstitutionAdmin" || 
            r.Role.Name == "InstitutionOwner");

        if (!user.EmailConfirmed && !isAdmin)
        {
            return Result.Failure<LoginResponse>(new Error("Auth.EmailNotConfirmed", "Lütfen e-posta adresinizi doğrulayın."));
        }
        
        // 5. MAINTENANCE MODE CHECK
        // Check if System is in Maintenance Mode (Global or Identity Service)
        // Admin users (SystemAdmin, InstitutionOwner) are exempt.
        if (!isAdmin)
        {
            var globalMaintenance = await _configurationService.GetConfigurationValueAsync("system.maintenancemode", cancellationToken);
            var identityMaintenance = await _configurationService.GetConfigurationValueAsync("maintenance.identity", cancellationToken);

            if (string.Equals(globalMaintenance, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identityMaintenance, "true", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<LoginResponse>(new Error("System.MaintenanceMode", "Sistem şu anda bakım modundadır. Lütfen daha sonra tekrar deneyiniz."));
            }
        }

        // 6. Generate Tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        
        var ipAddress = "0.0.0.0"; // Should be passed in command but defaulting here
        var refreshToken = _tokenService.GenerateRefreshToken(user.Id, ipAddress); 
        
        // 7. Save Refresh Token (Securely bypassing concurrency checks on User)
        var saveTokenResult = await _identityService.SaveRefreshTokenAsync(user.Id, refreshToken, cancellationToken);
        if (saveTokenResult.IsFailure)
        {
            _logger.LogError("Failed to save refresh token: {Error}", saveTokenResult.Error.Description);
            return Result.Failure<LoginResponse>(saveTokenResult.Error);
        }

        // 8. Record Login Stats (Best effort)
        try 
        {
            user.RecordLogin();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Login stats recording failed");
        }

        return Result.Success(new LoginResponse(accessToken, refreshToken.Token));
    }
}
