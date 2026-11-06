using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;

namespace Identity.Application.Services;

/// <summary>
/// Paket F L5 (E5 leftover): the signed-in account's current e-mail address, read from the database by the token's
/// subject. The token's <c>email</c> claim is not used: after a confirmed e-mail change it keeps the old address until
/// the session is refreshed. Null when the request is anonymous or the account no longer exists or is inactive
/// (deactivated or erased). A light, untracked read (PR #124 review L3).
/// </summary>
public static class CurrentUserEmail
{
    public static async Task<string?> ReadAsync(
        ICurrentUserService currentUser,
        IUserRepository users,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return null;
        var email = await users.GetActiveEmailAsync(userId, cancellationToken);
        return string.IsNullOrWhiteSpace(email) ? null : email;
    }
}
