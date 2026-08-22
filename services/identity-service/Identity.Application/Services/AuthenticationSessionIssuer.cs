using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Commands.Login;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Services;

public sealed class AuthenticationSessionIssuer : IAuthenticationSessionIssuer
{
    private readonly ITokenService _tokens;
    private readonly IIdentityService _identity;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuthenticationSessionIssuer> _logger;

    public AuthenticationSessionIssuer(
        ITokenService tokens,
        IIdentityService identity,
        IUnitOfWork unitOfWork,
        ILogger<AuthenticationSessionIssuer> logger)
    {
        _tokens = tokens;
        _identity = identity;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<LoginResponse>> IssueAsync(
        User user,
        bool rememberMe,
        string ipAddress,
        DateTimeOffset? mfaVerifiedAt,
        CancellationToken cancellationToken)
    {
        var accessToken = _tokens.GenerateAccessToken(user, mfaVerifiedAt);
        var refreshToken = _tokens.GenerateRefreshToken(user.Id, ipAddress, rememberMe, mfaVerifiedAt);
        var persisted = await _identity.SaveRefreshTokenAsync(user.Id, refreshToken, cancellationToken);
        if (persisted.IsFailure)
        {
            return Result.Failure<LoginResponse>(persisted.Error);
        }

        // The session issuer is the successful-authentication boundary for MFA
        // flows. Keep login activity independent from token persistence failures
        // so a non-critical stats write cannot invalidate an authenticated session.
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
            ExpiresInMinutes: _tokens.GetAccessTokenLifetimeMinutes()));
    }
}
