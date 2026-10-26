using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Authorization;
using Identity.Application.Authorization;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;
    private readonly SessionLifetimeOptions _sessionLifetime;

    public RefreshTokenCommandHandler(
        IUserRepository userRepository,
        ITokenService tokenService,
        IConfigurationService configurationService,
        ILogger<RefreshTokenCommandHandler> logger,
        SessionLifetimeOptions? sessionLifetime = null)
    {
        _sessionLifetime = sessionLifetime ?? new SessionLifetimeOptions();
        _userRepository = userRepository;
        _tokenService = tokenService;
        _configurationService = configurationService;
        _logger = logger;
    }

    public Task<Result<RefreshTokenResponse>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken) => HandleInternal(request, cancellationToken);

    private async Task<Result<RefreshTokenResponse>> HandleInternal(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByRefreshTokenAsync(request.RefreshToken, cancellationToken);
        
        if (user == null)
        {
            if (!string.IsNullOrEmpty(request.RefreshToken)
                && await _userRepository.RevokeRefreshTokenFamilyOnReuseAsync(request.RefreshToken, cancellationToken))
            {
                _logger.LogWarning("A rotated refresh token was presented again; the whole session was revoked.");
            }

            return Result.Failure<RefreshTokenResponse>(new Error("Auth.InvalidToken", "Geçersiz oturum anahtarı."));
        }

        var existingRefreshToken = user.RefreshTokens.FirstOrDefault(r => r.Matches(request.RefreshToken));

        if (existingRefreshToken == null || !existingRefreshToken.IsActive)
             return Result.Failure<RefreshTokenResponse>(new Error("Auth.InvalidToken", "Oturum süresi dolmuş veya geçersiz."));

        if (request.ExpectedUserId is { } expectedUserId && expectedUserId != user.Id)
        {
            return Result.Failure<RefreshTokenResponse>(new Error(
                "Auth.InvalidToken",
                "Oturum anahtarı artık geçerli değil; lütfen tekrar giriş yapın."));
        }

        var requestedProduct = request.TargetProduct;
        var targetProduct = requestedProduct ?? existingRefreshToken.Product;
        var isProductSwitch = requestedProduct.HasValue;
        if (requestedProduct is { } productToSwitchTo
            && StaffProductAccessPolicy.GetStaffRoles(user, productToSwitchTo).Count == 0
            && !(productToSwitchTo == Identity.Domain.Enums.PlatformProduct.CourseManagement
                && request.AllowCourseManagementStudentSession
                && user.HasProductAccess(Identity.Domain.Enums.PlatformProduct.CourseManagement)
                && user.GetRolesForProductScope(Identity.Domain.Enums.PlatformProduct.CourseManagement).Any(userRole =>
                    userRole.Role is not null
                    && !userRole.Role.IsDeleted
                    && string.Equals(userRole.Role.Name, "CourseStudent", StringComparison.OrdinalIgnoreCase))))
        {
            return Result.Failure<RefreshTokenResponse>(new Error(
                "Auth.ProductAccessDenied",
                "Bu platformda personel erişiminiz bulunmuyor."));
        }

        var productRoles = user.GetRolesForProductScope(targetProduct).ToArray();
        var isSystemAdministrator = productRoles.Any(role =>
            role.Role is not null
            && string.Equals(role.Role.Name, "SystemAdmin", StringComparison.OrdinalIgnoreCase));
        if (!isSystemAdministrator
            && (existingRefreshToken.Product is not { } product || !user.HasProductAccess(product)))
        {
            await _userRepository.RevokeRefreshTokenAsync(
                request.RefreshToken,
                "system",
                "product access is missing or revoked",
                cancellationToken);
            return Result.Failure<RefreshTokenResponse>(new Error(
                "Auth.ProductAccessDenied",
                "Bu platform için oturum erişiminiz yok. Lütfen yeniden giriş yapın."));
        }

        var isPrivilegedAdministrator = productRoles.Any(role =>
            role.Role is not null
            && (string.Equals(role.Role.Name, "SystemAdmin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role.Role.Name, "InstitutionAdmin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role.Role.Name, "InstitutionOwner", StringComparison.OrdinalIgnoreCase)
                || targetProduct == Identity.Domain.Enums.PlatformProduct.CourseManagement && string.Equals(role.Role.Name, "CourseInstitutionOwner", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role.Role.Name, "Editor", StringComparison.OrdinalIgnoreCase)));
        // A session opened without the second factor (for example before the account
        // enrolled, or before MFA applied to non-administrators) cannot be extended.
        if (user.MfaEnabled
            && await IsPrivilegedMfaRequiredAsync(cancellationToken))
        {
            if (existingRefreshToken.MfaVerifiedAt is null)
            {
                if (!isProductSwitch)
                {
                    var revoked = await _userRepository.RevokeRefreshTokenAsync(
                        request.RefreshToken,
                        "system",
                        "MFA reauthentication required",
                        cancellationToken);
                    if (!revoked)
                        return InvalidatedRefreshToken();
                }

                return Result.Failure<RefreshTokenResponse>(new Error(
                    "Auth.MfaRequired",
                    isProductSwitch
                        ? "Bu yönetici paneline geçmek için yeniden giriş yapıp iki adımlı doğrulamayı tamamlayın."
                        : isPrivilegedAdministrator
                            ? "Yönetici oturumunun iki adımlı doğrulamayla yeniden açılması gerekiyor."
                            : "Oturumunuzun iki adımlı doğrulamayla yeniden açılması gerekiyor."));
            }
        }

        // Generate the replacement before atomically revoking the old token.
        var newAccessToken = await _tokenService.GenerateAccessTokenAsync(
            user,
            existingRefreshToken.MfaVerifiedAt,
            targetProduct);
        var newRefreshToken = _tokenService.GenerateRefreshToken(
            user.Id,
            "0.0.0.0",
            existingRefreshToken.IsPersistent,
            existingRefreshToken.MfaVerifiedAt,
            targetProduct);
        // Same sign-in, same family; rotation never extends it past the absolute limit.
        newRefreshToken.ContinueSessionOf(existingRefreshToken, _sessionLifetime.MaxSessionLifetime);

        var rotated = await _userRepository.RotateRefreshTokenAsync(
            request.RefreshToken,
            newRefreshToken,
            "0.0.0.0",
            Identity.Domain.Entities.RefreshToken.RotatedReason,
            cancellationToken);
        if (!rotated)
        {
            return InvalidatedRefreshToken();
        }

        return Result.Success(new RefreshTokenResponse(
            newAccessToken,
            newRefreshToken.IssuedValue!,
            newRefreshToken.ExpiresAt,
            newRefreshToken.IsPersistent,
            ExpiresInMinutes: await _tokenService.GetAccessTokenLifetimeMinutesAsync()));
    }

    private static Result<RefreshTokenResponse> InvalidatedRefreshToken() =>
        Result.Failure<RefreshTokenResponse>(new Error(
            "Auth.InvalidToken",
            "Oturum anahtarı artık geçerli değil; lütfen tekrar giriş yapın."));

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
                "Privileged refresh MFA policy could not be read; keeping MFA required.");
            return true;
        }
    }
}
