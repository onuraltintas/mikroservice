using System.Security.Claims;
using Coaching.Application.Profiles;

namespace Coaching.API.Security;

/// <summary>
/// The name written on a payment notice and its sales documents. Live test 13.11.2026: the access token carries no name
/// claim, so every contract and every admin list said "Koçluk kullanıcısı". The name comes from the account directory
/// when the token has none.
/// </summary>
internal static class CoachingBuyerName
{
    public static async Task<string?> ResolveAsync(HttpContext http, Guid userId, CancellationToken cancellationToken)
    {
        var user = http.User;
        var claimed = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("name");
        if (string.IsNullOrWhiteSpace(claimed))
        {
            claimed = string.Join(' ', new[]
            {
                user.FindFirstValue(ClaimTypes.GivenName) ?? user.FindFirstValue("given_name"),
                user.FindFirstValue(ClaimTypes.Surname) ?? user.FindFirstValue("family_name")
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }
        if (!string.IsNullOrWhiteSpace(claimed)) return claimed.Trim();
        if (http.RequestServices?.GetService(typeof(ICoachingAccountDirectory)) is not ICoachingAccountDirectory accounts) return null;
        var account = (await accounts.GetAccountsAsync([userId], cancellationToken)).FirstOrDefault(item => item.UserId == userId);
        var name = account is null ? null : $"{account.FirstName} {account.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
