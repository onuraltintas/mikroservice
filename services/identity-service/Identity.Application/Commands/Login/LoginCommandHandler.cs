using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
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
    private readonly IMultiFactorService _multiFactorService;
    private readonly ILoginAttemptLimiter _loginAttempts;
    private readonly Microsoft.Extensions.Logging.ILogger<LoginCommandHandler> _logger;

    // Verified against when the address is unknown, so both outcomes cost one full
    // key derivation and the response time does not reveal which accounts exist.
    private static readonly object DummyCredentialLock = new();
    private static (byte[] Hash, byte[] Salt)? _dummyCredential;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        IIdentityService identityService,
        IConfigurationService configurationService,
        IMultiFactorService multiFactorService,
        Microsoft.Extensions.Logging.ILogger<LoginCommandHandler> logger,
        ILoginAttemptLimiter? loginAttempts = null)
    {
        _loginAttempts = loginAttempts ?? NoLoginAttemptLimiter.Instance;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _identityService = identityService;
        _configurationService = configurationService;
        _multiFactorService = multiFactorService;
        _logger = logger;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // 0. Per-account lockout (keyed by address, identical for unknown accounts).
        var lockout = await _loginAttempts.GetLockoutAsync(request.Email, cancellationToken);
        if (lockout is { } remaining)
        {
            return Result.Failure<LoginResponse>(TooManyAttempts(remaining));
        }

        // 1. Get User
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user == null)
        {
            var dummy = GetDummyCredential();
            _passwordHasher.VerifyPasswordHash(request.Password, dummy.Hash, dummy.Salt);
            await _loginAttempts.RecordFailureAsync(request.Email, cancellationToken);
            return Result.Failure<LoginResponse>(new Error("Auth.InvalidCredentials", "E-posta veya şifre hatalı."));
        }

        // 2. Verify Password
        if (!_passwordHasher.VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            await _loginAttempts.RecordFailureAsync(request.Email, cancellationToken);
            return Result.Failure<LoginResponse>(new Error("Auth.InvalidCredentials", "E-posta veya şifre hatalı."));
        }

        await _loginAttempts.ResetAsync(request.Email, cancellationToken);

        var passwordRehashed = _passwordHasher.NeedsRehash(user.PasswordHash, user.PasswordSalt);
        if (passwordRehashed)
        {
            _passwordHasher.CreatePasswordHash(request.Password, out var passwordHash, out var passwordSalt);
            user.SetPassword(passwordHash, passwordSalt);
        }

        // 3. Check Active Status
        if (!user.IsActive)
        {
             return Result.Failure<LoginResponse>(new Error("Auth.UserInactive", "Hesabınız pasif durumdadır."));
        }

        // 4. Check Email Confirmation & IsAdmin Check
        var productRoles = user.GetRolesForProductScope(request.Product).ToArray();
        var isAdmin = productRoles.Any(r => 
            r.Role.Name == "SystemAdmin" || 
            r.Role.Name == "InstitutionAdmin" || 
            r.Role.Name == "InstitutionOwner" ||
            request.Product == PlatformProduct.CourseManagement && r.Role.Name == "CourseInstitutionOwner" ||
            r.Role.Name == "Editor");

        var isSystemAdministrator = productRoles.Any(role =>
            string.Equals(role.Role.Name, "SystemAdmin", StringComparison.OrdinalIgnoreCase));

        if (request.Product.HasValue && !Enum.IsDefined(request.Product.Value))
        {
            return Result.Failure<LoginResponse>(new Error("Auth.InvalidProduct", "Geçersiz platform."));
        }

        if (!isSystemAdministrator
            && (request.Product is not { } product || !user.HasProductAccess(product)))
        {
            return Result.Failure<LoginResponse>(new Error(
                "Auth.ProductAccessDenied",
                "Bu platform için hesabınıza erişim yetkisi tanımlı değil."));
        }

        if (!user.EmailConfirmed && !isSystemAdministrator)
        {
            return Result.Failure<LoginResponse>(new Error("Auth.EmailNotConfirmed", "Lütfen e-posta adresinizi doğrulayın."));
        }
        
        // 5. MAINTENANCE MODE CHECK
        // Check if System is in Maintenance Mode (Global or Identity Service)
        // Privileged administrators are exempt.
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

        // Every account that enrolled an authenticator gets the second factor,
        // not only administrators. The central "system" policy stays the single
        // break-glass switch that suspends login challenges platform-wide.
        if (user.MfaEnabled
            && await IsPrivilegedMfaRequiredAsync(cancellationToken))
        {
            if (passwordRehashed)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Success(LoginResponse.RequireMfa(
                _multiFactorService.CreateChallenge(user.Id, request.RememberMe, request.Product),
                enrollmentRequired: !user.MfaEnabled));
        }

        // 6. Generate Tokens
        var accessToken = await _tokenService.GenerateAccessTokenAsync(user, product: request.Product);
        
        var ipAddress = "0.0.0.0"; // Should be passed in command but defaulting here
        var refreshToken = _tokenService.GenerateRefreshToken(
            user.Id,
            ipAddress,
            request.RememberMe,
            product: request.Product);
        
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

        return Result.Success(new LoginResponse(
            accessToken,
            refreshToken.IssuedValue!,
            refreshToken.ExpiresAt,
            refreshToken.IsPersistent,
            ExpiresInMinutes: await _tokenService.GetAccessTokenLifetimeMinutesAsync()));
    }

    internal static Error TooManyAttempts(TimeSpan remaining) => new(
        "Auth.TooManyAttempts",
        $"Çok fazla başarısız giriş denemesi. Lütfen {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))} dakika sonra tekrar deneyin.");

    private (byte[] Hash, byte[] Salt) GetDummyCredential()
    {
        if (_dummyCredential is { } existing) return existing;
        lock (DummyCredentialLock)
        {
            if (_dummyCredential is null)
            {
                _passwordHasher.CreatePasswordHash(Guid.NewGuid().ToString("N"), out var hash, out var salt);
                _dummyCredential = (hash, salt);
            }

            return _dummyCredential.Value;
        }
    }

    private async Task<bool> IsPrivilegedMfaRequiredAsync(CancellationToken cancellationToken)
    {
        try
        {
            var mode = await _configurationService.GetConfigurationValueAsync(
                MfaOperationCategories.ConfigurationKey(MfaOperationCategories.System),
                cancellationToken);
            return !string.Equals(mode, MfaPolicyModes.Disabled, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Privileged login MFA policy could not be read; keeping MFA required.");
            return true;
        }
    }
}
