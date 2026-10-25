using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Interfaces;

public interface ITokenService
{
    Task<string> GenerateAccessTokenAsync(
        User user,
        DateTimeOffset? mfaVerifiedAt = null,
        PlatformProduct? product = null);
    Task<int> GetAccessTokenLifetimeMinutesAsync();
    RefreshToken GenerateRefreshToken(
        Guid userId,
        string ipAddress,
        bool isPersistent = true,
        DateTimeOffset? mfaVerifiedAt = null,
        PlatformProduct? product = null);
}
