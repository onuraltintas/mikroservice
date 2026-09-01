using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;

namespace Identity.Application.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;

    public RefreshTokenCommandHandler(
        IUserRepository userRepository,
        ITokenService tokenService)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
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
            return Result.Failure<RefreshTokenResponse>(new Error("Auth.InvalidToken", "Geçersiz oturum anahtarı."));

        var existingRefreshToken = user.RefreshTokens.FirstOrDefault(r => r.Token == request.RefreshToken);

        if (existingRefreshToken == null || !existingRefreshToken.IsActive)
             return Result.Failure<RefreshTokenResponse>(new Error("Auth.InvalidToken", "Oturum süresi dolmuş veya geçersiz."));

        var isSystemAdministrator = user.Roles.Any(role =>
            role.Role is not null
            && string.Equals(role.Role.Name, "SystemAdmin", StringComparison.OrdinalIgnoreCase));
        if (isSystemAdministrator && user.MfaEnabled && existingRefreshToken.MfaVerifiedAt is null)
        {
            var revoked = await _userRepository.RevokeRefreshTokenAsync(
                request.RefreshToken,
                "system",
                "MFA reauthentication required",
                cancellationToken);
            if (!revoked)
                return InvalidatedRefreshToken();

            return Result.Failure<RefreshTokenResponse>(new Error(
                "Auth.MfaRequired",
                "Yönetici oturumunun iki adımlı doğrulamayla yeniden açılması gerekiyor."));
        }

        // Generate the replacement before atomically revoking the old token.
        var newAccessToken = _tokenService.GenerateAccessToken(user, existingRefreshToken.MfaVerifiedAt);
        var newRefreshToken = _tokenService.GenerateRefreshToken(
            user.Id,
            "0.0.0.0",
            existingRefreshToken.IsPersistent,
            existingRefreshToken.MfaVerifiedAt);
        
        var rotated = await _userRepository.RotateRefreshTokenAsync(
            request.RefreshToken,
            newRefreshToken,
            "0.0.0.0",
            "Refreshed",
            cancellationToken);
        if (!rotated)
        {
            return InvalidatedRefreshToken();
        }

        return Result.Success(new RefreshTokenResponse(
            newAccessToken,
            newRefreshToken.Token,
            newRefreshToken.ExpiresAt,
            newRefreshToken.IsPersistent,
            ExpiresInMinutes: _tokenService.GetAccessTokenLifetimeMinutes()));
    }

    private static Result<RefreshTokenResponse> InvalidatedRefreshToken() =>
        Result.Failure<RefreshTokenResponse>(new Error(
            "Auth.InvalidToken",
            "Oturum anahtarı artık geçerli değil; lütfen tekrar giriş yapın."));
}
