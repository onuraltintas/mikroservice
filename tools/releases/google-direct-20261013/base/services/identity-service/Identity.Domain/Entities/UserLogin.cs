using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Domain.Entities;

/// <summary>
/// Harici giriş sağlayıcıları (Google, Facebook vb.)
/// </summary>
public class UserLogin : Entity
{
    public Guid UserId { get; private set; }
    public string LoginProvider { get; private set; } = string.Empty;
    public string ProviderKey { get; private set; } = string.Empty;
    public string? ProviderDisplayName { get; private set; }

    // Navigation property
    // public User User { get; private set; } // EF Core tarafından yönetilecek

    private UserLogin() { }

    public static UserLogin Create(Guid userId, string loginProvider, string providerKey, string? providerDisplayName)
    {
        return new UserLogin
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LoginProvider = loginProvider,
            ProviderKey = providerKey,
            ProviderDisplayName = providerDisplayName,
            CreatedAt = DateTime.UtcNow
        };
    }
}
