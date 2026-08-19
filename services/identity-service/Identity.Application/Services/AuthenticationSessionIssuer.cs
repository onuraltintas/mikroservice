using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Commands.Login;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;

namespace Identity.Application.Services;

public sealed class AuthenticationSessionIssuer : IAuthenticationSessionIssuer
{
    private readonly ITokenService _tokens;
    private readonly IIdentityService _identity;

    public AuthenticationSessionIssuer(ITokenService tokens, IIdentityService identity)
    {
        _tokens = tokens;
        _identity = identity;
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
        return persisted.IsSuccess
            ? Result.Success(new LoginResponse(
                accessToken,
                refreshToken.Token,
                refreshToken.ExpiresAt,
                refreshToken.IsPersistent,
                ExpiresInMinutes: _tokens.GetAccessTokenLifetimeMinutes()))
            : Result.Failure<LoginResponse>(persisted.Error);
    }
}
