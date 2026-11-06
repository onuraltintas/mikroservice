using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;

namespace Identity.Application.Services;

/// <summary>
/// Paket F L5 (E5 leftover): the signed-in account's current e-mail address, read from the database by the token's
/// subject. The token's <c>email</c> claim is not used: after a confirmed e-mail change it keeps the old address until
/// the session is refreshed. Null when the request is anonymous or the account no longer exists.
/// </summary>
public static class CurrentUserEmail
{
    public static async Task<string?> ReadAsync(
        ICurrentUserService currentUser,
        IUserRepository users,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return null;
        var user = await users.GetByIdAsync(userId, cancellationToken);
        return user is null || string.IsNullOrWhiteSpace(user.Email) ? null : user.Email;
    }
}
