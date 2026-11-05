using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Services;

/// <summary>
/// Paket A review #3: the real owner proves an unconfirmed address with a verified external login.
/// Besides the credentials (<see cref="User.ClaimUnconfirmedAccountByVerifiedExternalLogin"/>), the
/// services joined while the address was unconfirmed are removed, including the institution links
/// those joins created. The caller saves the changes.
/// </summary>
public static class UnconfirmedAccountClaim
{
    public static async Task<IReadOnlyList<PlatformProduct>> ClaimAsync(User user, IInstitutionRepository institutions,
        CancellationToken cancellationToken)
    {
        var revoked = user.ClaimUnconfirmedAccountByVerifiedExternalLogin();
        if (revoked.Count == 0)
            return revoked;

        // Second review L4/M1: only the links those joins made are removed; links others created stay.
        var links = await institutions.GetAdminsForRegistrationAsync(user.Id, cancellationToken);
        foreach (var link in links.Where(link => link.UserId == user.Id && link.IsExistingAccountJoinLink()
            && link.Product is { } product && revoked.Contains(product)).ToList())
            institutions.RemoveAdmin(link);
        return revoked;
    }
}
