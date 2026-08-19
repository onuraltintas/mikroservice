using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.Login;

public record LoginCommand(
    string Email,
    string Password,
    bool RememberMe = true) : IRequest<Result<LoginResponse>>;

public record LoginResponse(
    string? AccessToken,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAt,
    bool IsPersistent,
    string TokenType = "Bearer",
    int ExpiresInMinutes = 15,
    bool RequiresMfa = false,
    bool MfaEnrollmentRequired = false,
    string? MfaChallengeToken = null)
{
    public static LoginResponse RequireMfa(string challengeToken, bool enrollmentRequired) =>
        new(
            AccessToken: null,
            RefreshToken: null,
            RefreshTokenExpiresAt: null,
            IsPersistent: false,
            RequiresMfa: true,
            MfaEnrollmentRequired: enrollmentRequired,
            MfaChallengeToken: challengeToken);
}
