using Identity.Domain.Entities;

namespace Identity.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user, DateTimeOffset? mfaVerifiedAt = null);
    int GetAccessTokenLifetimeMinutes();
    RefreshToken GenerateRefreshToken(Guid userId, string ipAddress, bool isPersistent = true, DateTimeOffset? mfaVerifiedAt = null);
}
