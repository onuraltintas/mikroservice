using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Domain.Entities;

/// <summary>
/// User entity - Managed locally
/// </summary>
public class User : AggregateRoot
{
    public string Email { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    
    // Auth credentials
    public byte[] PasswordHash { get; private set; } = Array.Empty<byte>();
    public byte[] PasswordSalt { get; private set; } = Array.Empty<byte>();

    public bool EmailConfirmed { get; private set; }
    public string? EmailVerificationToken { get; private set; }
    public DateTime? EmailVerificationTokenExpiresAt { get; private set; }

    public string? PasswordResetToken { get; private set; }
    public DateTime? PasswordResetTokenExpiresAt { get; private set; }

    public string? PhoneNumber { get; private set; }
    public bool PhoneConfirmed { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? LastLoginAt { get; private set; }

    // Navigation properties
    private readonly List<UserRole> _roles = new();
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();
    
    private readonly List<RefreshToken> _refreshTokens = new();
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    private readonly List<UserLogin> _logins = new();
    public IReadOnlyCollection<UserLogin> Logins => _logins.AsReadOnly();

    public StudentProfile? StudentProfile { get; private set; }
    public TeacherProfile? TeacherProfile { get; private set; }
    public ParentProfile? ParentProfile { get; private set; }

    private User() { } // EF Core

    public User(Guid id, string email, string firstName, string lastName) : base(id)
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
        FirstName = firstName;
        LastName = lastName;
    }

    public static User Create(Guid id, string email, string firstName = "", string lastName = "")
    {
        var user = new User(id, email, firstName, lastName);
        // Domain event can be raised
        return user;
    }

    public void SetPassword(byte[] passwordHash, byte[] passwordSalt)
    {
        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void AddRefreshToken(RefreshToken refreshToken)
    {
        _refreshTokens.Add(refreshToken);
    }

    public void AddLogin(UserLogin login)
    {
        if (!_logins.Any(x => x.LoginProvider == login.LoginProvider && x.ProviderKey == login.ProviderKey))
        {
            _logins.Add(login);
        }
    }

    public void RevokeRefreshToken(string token, string ipAddress, string reason)
    {
        var refreshToken = _refreshTokens.SingleOrDefault(t => t.Token == token);
        if (refreshToken != null && refreshToken.IsActive)
        {
            refreshToken.Revoke(ipAddress, reason);
        }
    }

    public void UpdateName(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConfirmEmail()
    {
        EmailConfirmed = true;
        EmailVerificationToken = null;
        EmailVerificationTokenExpiresAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void GenerateEmailVerificationToken()
    {
        EmailVerificationToken = Guid.NewGuid().ToString("N");
        EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24);
        UpdatedAt = DateTime.UtcNow;
    }

    public void GeneratePasswordResetToken()
    {
        PasswordResetToken = Guid.NewGuid().ToString("N");
        PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(2); // Short lived
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearPasswordResetToken()
    {
        PasswordResetToken = null;
        PasswordResetTokenExpiresAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPhoneNumber(string phoneNumber, bool confirmed = false)
    {
        PhoneNumber = phoneNumber;
        PhoneConfirmed = confirmed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddRole(UserRole userRole)
    {
        if (!_roles.Any(r => r.RoleId == userRole.RoleId))
        {
            _roles.Add(userRole);
        }
    }

    public void RemoveRole(Guid roleId)
    {
        var role = _roles.FirstOrDefault(r => r.RoleId == roleId);
        if (role != null)
        {
            _roles.Remove(role);
        }
    }
}

/// <summary>
/// Kullanıcı rolü
/// </summary>
public class UserRole : Entity<Guid>
{
    public Guid UserId { get; private set; }
    // public User User { get; private set; } = null!; // Circular reference warning in some contexts, but EF handles it.

    public Guid RoleId { get; private set; }
    public Role Role { get; private set; } = null!;

    private UserRole() { }

    public UserRole(Guid userId, Guid roleId)
    {
        UserId = userId;
        RoleId = roleId;
    }
}
