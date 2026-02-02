using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Commands.Login;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Commands.GoogleLogin;

public class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, Result<LoginResponse>>
{
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdentityService _identityService;
    private readonly IStudentRepository _studentRepository;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<GoogleLoginCommandHandler> _logger;

    public GoogleLoginCommandHandler(
        IGoogleAuthService googleAuthService,
        IUserRepository userRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        IIdentityService identityService,
        IStudentRepository studentRepository,
        IConfigurationService configurationService,
        ILogger<GoogleLoginCommandHandler> logger)
    {
        _googleAuthService = googleAuthService;
        _userRepository = userRepository;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _identityService = identityService;
        _studentRepository = studentRepository;
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task<Result<LoginResponse>> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify Google Token
        var googleUser = await _googleAuthService.VerifyGoogleTokenAsync(request.IdToken);
        if (googleUser == null)
        {
            _logger.LogWarning("Google Login Failed: Invalid Google Token provided.");
            return Result.Failure<LoginResponse>(new Error("Auth.InvalidToken", "Invalid Google ID Token."));
        }

        // 2. Check if user exists
        var user = await _userRepository.GetByEmailAsync(googleUser.Email, cancellationToken);
        
        if (user == null)
        {
            // NEW USER REGISTRATION CHECK
            var allowRegistration = await _configurationService.GetConfigurationValueAsync("auth.allowregistration", cancellationToken);
            if (!string.Equals(allowRegistration, "true", StringComparison.OrdinalIgnoreCase))
            {
                 _logger.LogWarning("Google Login Blocked: Registration is disabled and user {Email} does not exist.", googleUser.Email);
                 return Result.Failure<LoginResponse>(new Error("Identity.RegistrationDisabled", "Yeni kullanıcı kayıtları kapalıdır. Mevcut bir hesabınız yoksa giriş yapamazsınız."));
            }

            // 2b. Auto-Register User
            _logger.LogInformation("Google Login: Registering new user {Email}", googleUser.Email);

            var randomPassword = Guid.NewGuid().ToString("N") + "A1!"; // Unused but required
            
            var regResult = await _identityService.RegisterUserAsync(
                googleUser.Email, 
                randomPassword, 
                googleUser.FirstName, 
                googleUser.LastName, 
                cancellationToken);

            Guid userId;
            if (regResult.IsFailure)
            {
                // Handle Race Condition: If user was created by another request in the meantime
                if (regResult.Error.Code == "Identity.UserExists")
                {
                    _logger.LogInformation("Google Login: Race condition detected for {Email}, retrieving existing user.", googleUser.Email);
                    user = await _userRepository.GetByEmailAsync(googleUser.Email, cancellationToken);
                     if (user == null) 
                    {
                         _logger.LogError("Google Login Error: User exists reported but retrieval returned null for {Email}", googleUser.Email);
                         return Result.Failure<LoginResponse>(new Error("Auth.UserNotFoundAfterRace", "User exists but could not be retrieved."));
                    }
                    userId = user.Id;
                }
                else
                {
                    _logger.LogError("Google Login Registration Failed: {Error}", regResult.Error.Description);
                    return Result.Failure<LoginResponse>(regResult.Error);
                }
            }
            else 
            {
               userId = regResult.Value;
               // Reload user to get fresh entity
               user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            }
            
            // 2b-bis. Post-Creation Setup (Only if we have a user now)
            if (user != null)
            {
                // Ensure Email Checked (Google Trusted) & User Active
                if (!user.EmailConfirmed || !user.IsActive)
                {
                    if (!user.EmailConfirmed) user.ConfirmEmail();
                    if (!user.IsActive) user.Activate();
                    
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                // If freshly registered (regResult.IsSuccess), do roles & profile setup
                if (regResult.IsSuccess)
                {
                    _logger.LogInformation("Google Login: Setting up profile/roles for new user {UserId}", userId);
                    
                    // Assign Role: Student (Default)
                    await _identityService.AssignRoleAsync(userId, Identity.Domain.Enums.UserRole.Student.ToString(), cancellationToken);
                    
                    // Create Student Profile
                    var student = StudentProfile.Create(userId, googleUser.FirstName, googleUser.LastName);
                    if (!string.IsNullOrEmpty(googleUser.PictureUrl)) student.SetAvatar(googleUser.PictureUrl);
                    
                    await _studentRepository.AddAsync(student, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }
        } 
        else 
        {
            // User existed
             if (!user.IsActive)
            {
                 _logger.LogWarning("Google Login: User {Email} is inactive.", googleUser.Email);
                 return Result.Failure<LoginResponse>(new Error("Auth.UserInactive", "User account is inactive."));
            }
        }

        // Final Safety Check
        if (user == null)
        {
             user = await _userRepository.GetByEmailAsync(googleUser.Email, cancellationToken);
             if (user == null)
             {
                 _logger.LogError("Google Login Critical: User lookup failed at end of flow for {Email}", googleUser.Email);
                 return Result.Failure<LoginResponse>(new Error("Auth.UserCreationFailed", "Could not retrieve user."));
             }
        }

        // MAINTENANCE MODE CHECK (Google users can be logged in now, check roles)
        var isAdmin = user.Roles.Any(r => 
            r.Role.Name == "SystemAdmin" || 
            r.Role.Name == "InstitutionAdmin" || 
            r.Role.Name == "InstitutionOwner");

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

        // 3. Generate Tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken(user.Id, request.IpAddress);

        // 4. Save Refresh Token (Using Safe Method)
        var saveTokenResult = await _identityService.SaveRefreshTokenAsync(user.Id, refreshToken, cancellationToken);
        
        if (saveTokenResult.IsFailure)
        {
             _logger.LogError("Google Login: Failed to save refresh token. Error: {Error}", saveTokenResult.Error.Description);
             return Result.Failure<LoginResponse>(saveTokenResult.Error);
        }

        return Result.Success(new LoginResponse(accessToken, refreshToken.Token));
    }
}
