using EduPlatform.Shared.Kernel.Results;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.Login;

public record LoginCommand(
    string Email,
    string Password,
    bool RememberMe = true,
    PlatformProduct? Product = null,
    StaffSignIn? Staff = null) : IRequest<Result<LoginResponse>>;

/// <summary>
/// User decision 23.11.2026: the staff portal no longer asks which product to open. The service opens the product where
/// the account holds a role for this address (<paramref name="Workspace"/>: "teacher" or "institution"); with two such
/// products the last used one (<paramref name="PreferredProduct"/>), otherwise maxkoç.
/// </summary>
public record StaffSignIn(string? Workspace, PlatformProduct? PreferredProduct);

public record LoginResponse(
    string? AccessToken,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAt,
    bool IsPersistent,
    string TokenType = "Bearer",
    int ExpiresInMinutes = 15,
    bool RequiresMfa = false,
    bool MfaEnrollmentRequired = false,
    string? MfaChallengeToken = null,
    string? Product = null)
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
