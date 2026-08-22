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
    private readonly IMultiFactorService _multiFactorService;
    private readonly ILogger<GoogleLoginCommandHandler> _logger;

    public GoogleLoginCommandHandler(
        IGoogleAuthService googleAuthService,
        IUserRepository userRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        IIdentityService identityService,
        IStudentRepository studentRepository,
        IConfigurationService configurationService,
        IMultiFactorService multiFactorService,
        ILogger<GoogleLoginCommandHandler> logger)
    {
        _googleAuthService = googleAuthService;
        _userRepository = userRepository;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _identityService = identityService;
        _studentRepository = studentRepository;
        _configurationService = configurationService;
        _multiFactorService = multiFactorService;
        _logger = logger;
    }

    public async Task<Result<LoginResponse>> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify Google Token
        var googleUser = await _googleAuthService.VerifyGoogleTokenAsync(request.IdToken);
        if (googleUser is null || !GoogleAuthenticationRules.IsVerifiedGoogleUser(googleUser))
        {
            _logger.LogWarning("Google login failed security validation.");
            return Result.Failure<LoginResponse>(new Error("Auth.InvalidToken", "Invalid Google ID Token."));
        }

        // Google subject is the stable external identifier. Email is only used for
        // first-login account linking/registration after the ID token is verified.
        var user = await _userRepository.GetByLoginAsync(
            GoogleAuthenticationRules.LoginProvider,
            googleUser.GoogleId,
            cancellationToken);
        var userWasLinkedByGoogleSubject = user is not null;

        if (user is null)
        {
            user = await _userRepository.GetByEmailAsync(googleUser.Email, cancellationToken);
        }
        
        if (user == null)
        {
            // NEW USER REGISTRATION CHECK
            var allowRegistration = await _configurationService.GetConfigurationValueAsync("auth.allowregistration", cancellationToken);
            if (!string.Equals(allowRegistration, "true", StringComparison.OrdinalIgnoreCase))
            {
                 _logger.LogWarning("Google login blocked because registration is disabled.");
                 return Result.Failure<LoginResponse>(new Error("Identity.RegistrationDisabled", "Yeni kullanıcı kayıtları kapalıdır. Mevcut bir hesabınız yoksa giriş yapamazsınız."));
            }

            // 2b. Auto-Register User
            _logger.LogInformation("Google login is registering a new user.");

            var randomPassword = Guid.NewGuid().ToString("N") + "A1!"; // Unused but required
            
            var regResult = await _identityService.RegisterUserAsync(
                googleUser.Email, 
                randomPassword, 
                googleUser.FirstName, 
                googleUser.LastName, 
                cancellationToken);

            Guid userId;
            var createdNewUser = false;
            if (regResult.IsFailure)
            {
                // Handle Race Condition: If user was created by another request in the meantime
                if (regResult.Error.Code == "Identity.UserExists")
                {
                    _logger.LogInformation("Google login registration raced with another user creation; reloading the account.");
                    user = await _userRepository.GetByEmailAsync(googleUser.Email, cancellationToken);
                     if (user == null) 
                    {
                         _logger.LogError("Google login could not reload the account after a user-exists response.");
                         return Result.Failure<LoginResponse>(new Error("Auth.UserNotFoundAfterRace", "User exists but could not be retrieved."));
                    }
                    userId = user.Id;

                    if (!user.IsActive)
                    {
                        _logger.LogWarning("Google login rejected an inactive account after a registration race.");
                        return Result.Failure<LoginResponse>(new Error("Auth.UserInactive", "User account is inactive."));
                    }
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
               createdNewUser = true;
               // Reload user to get fresh entity
               user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            }
            
            // 2b-bis. Post-Creation Setup (Only if we have a user now)
            if (user != null)
            {
                if (!createdNewUser && GoogleAuthenticationRules.RequiresExplicitLink(user))
                {
                    return Result.Failure<LoginResponse>(new Error(
                        "Auth.ExternalLoginLinkRequired",
                        "Bu hesap için Google girişini önce hesap ayarlarından bağlamalısınız."));
                }

                var linkedGoogleUser = await _userRepository.GetByLoginAsync(
                    GoogleAuthenticationRules.LoginProvider,
                    googleUser.GoogleId,
                    cancellationToken);
                if (linkedGoogleUser is not null && linkedGoogleUser.Id != user.Id)
                {
                    return Result.Failure<LoginResponse>(new Error(
                        "Auth.ExternalLoginAlreadyLinked",
                        "Google hesabı başka bir kullanıcıya bağlıdır."));
                }

                var userChanged = false;
                UserLogin? googleLogin = null;
                if (linkedGoogleUser is null)
                {
                    googleLogin = UserLogin.Create(
                        user.Id,
                        GoogleAuthenticationRules.LoginProvider,
                        googleUser.GoogleId,
                        GoogleAuthenticationRules.LoginProvider);
                    userChanged = true;
                }

                // Only a newly created Google account may be activated here. A
                // registration race must never reactivate an administrator-disabled account.
                if (createdNewUser && (!user.EmailConfirmed || !user.IsActive))
                {
                    if (!user.EmailConfirmed) user.ConfirmEmail();
                    if (!user.IsActive) user.Activate();
                    userChanged = true;
                }

                if (userChanged)
                {
                    try
                    {
                        if (googleLogin is not null)
                        {
                            var loginAdded = await _userRepository.TryAddLoginAsync(
                                user,
                                googleLogin,
                                cancellationToken);
                            if (!loginAdded)
                            {
                                if (createdNewUser)
                                {
                                    await CompensateProvisionedUserAsync(userId);
                                }

                                return Result.Failure<LoginResponse>(new Error(
                                    "Auth.ExternalLoginAlreadyLinked",
                                    "Google hesabı başka bir kullanıcıya bağlıdır."));
                            }
                        }
                        else
                        {
                            await _unitOfWork.SaveChangesAsync(cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Google login could not persist the external login for {UserId}.", userId);
                        if (createdNewUser)
                        {
                            await CompensateProvisionedUserAsync(userId);
                        }

                        return Result.Failure<LoginResponse>(new Error(
                            "Auth.ExternalLoginPersistenceFailed",
                            "Google hesabı bağlanamadı."));
                    }
                }

                // If freshly registered (regResult.IsSuccess), do roles & profile setup
                if (regResult.IsSuccess)
                {
                    _logger.LogInformation("Google Login: Setting up profile/roles for new user {UserId}", userId);
                    
                    // Assign Role: Student (Default)
                    var roleResult = await _identityService.AssignRoleAsync(
                        userId,
                        Identity.Domain.Enums.UserRole.Student.ToString(),
                        cancellationToken);
                    if (roleResult.IsFailure)
                    {
                        _logger.LogError("Google login could not assign the default Student role for {UserId}.", userId);
                        await CompensateProvisionedUserAsync(userId);
                        return Result.Failure<LoginResponse>(roleResult.Error);
                    }
                    
                    // Create Student Profile
                    try
                    {
                        var student = StudentProfile.Create(userId, googleUser.FirstName, googleUser.LastName);
                        if (!string.IsNullOrEmpty(googleUser.PictureUrl)) student.SetAvatar(googleUser.PictureUrl);

                        await _studentRepository.AddAsync(student, cancellationToken);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Google login could not create the Student profile for {UserId}.", userId);
                        await CompensateProvisionedUserAsync(userId);
                        return Result.Failure<LoginResponse>(new Error(
                            "Auth.UserProvisioningFailed",
                            "Google hesabı için öğrenci profili oluşturulamadı."));
                    }
                }
            }
        } 
        else
        {
            if (!user.IsActive)
            {
                _logger.LogWarning("Google login rejected an inactive account.");
                return Result.Failure<LoginResponse>(new Error("Auth.UserInactive", "User account is inactive."));
            }

            if (!userWasLinkedByGoogleSubject && GoogleAuthenticationRules.RequiresExplicitLink(user))
            {
                return Result.Failure<LoginResponse>(new Error(
                    "Auth.ExternalLoginLinkRequired",
                    "Bu hesap için Google girişini önce hesap ayarlarından bağlamalısınız."));
            }

            // A verified Google email may link an existing local account on its
            // first Google login. Future logins use the provider subject above.
            if (!userWasLinkedByGoogleSubject
                && !user.Logins.Any(login =>
                    login.LoginProvider == GoogleAuthenticationRules.LoginProvider
                    && login.ProviderKey == googleUser.GoogleId))
            {
                if (!user.EmailConfirmed)
                {
                    user.ConfirmEmail();
                }

                try
                {
                    var loginAdded = await _userRepository.TryAddLoginAsync(
                        user,
                        UserLogin.Create(
                            user.Id,
                            GoogleAuthenticationRules.LoginProvider,
                            googleUser.GoogleId,
                            GoogleAuthenticationRules.LoginProvider),
                        cancellationToken);
                    if (!loginAdded)
                    {
                        return Result.Failure<LoginResponse>(new Error(
                            "Auth.ExternalLoginAlreadyLinked",
                            "Google hesabı başka bir kullanıcıya bağlıdır."));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Google login could not persist the external login for {UserId}.", user.Id);
                    return Result.Failure<LoginResponse>(new Error(
                        "Auth.ExternalLoginPersistenceFailed",
                        "Google hesabı bağlanamadı."));
                }
            }
        }

        // Final Safety Check
        if (user == null)
        {
             user = await _userRepository.GetByEmailAsync(googleUser.Email, cancellationToken);
             if (user == null)
             {
                 _logger.LogError("Google login could not reload the account at the end of the flow.");
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

        var isSystemAdministrator = user.Roles.Any(role =>
            string.Equals(role.Role.Name, "SystemAdmin", StringComparison.OrdinalIgnoreCase));
        if (isSystemAdministrator)
        {
            return Result.Success(LoginResponse.RequireMfa(
                _multiFactorService.CreateChallenge(user.Id, rememberMe: true),
                enrollmentRequired: !user.MfaEnabled));
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

        // Google login issues a session directly for non-administrator users.
        // Record the successful authentication here; MFA-protected accounts are
        // recorded by AuthenticationSessionIssuer after MFA completion.
        try
        {
            user.RecordLogin();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login stats recording failed for {UserId}.", user.Id);
        }

        return Result.Success(new LoginResponse(
            accessToken,
            refreshToken.Token,
            refreshToken.ExpiresAt,
            refreshToken.IsPersistent,
            ExpiresInMinutes: _tokenService.GetAccessTokenLifetimeMinutes()));
    }

    private async Task CompensateProvisionedUserAsync(Guid userId)
    {
        using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        try
        {
            _unitOfWork.ClearTracking();
            var cleanupResult = await _identityService.DeleteUserAsync(userId, cleanupTimeout.Token);
            if (cleanupResult.IsFailure)
            {
                _logger.LogError(
                    "Google login compensation failed for provisioned user {UserId}: {ErrorCode}.",
                    userId,
                    cleanupResult.Error.Code);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Google login compensation threw for provisioned user {UserId}.",
                userId);
        }
    }
}
