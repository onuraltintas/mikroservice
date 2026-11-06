using EduPlatform.Shared.Kernel.Primitives;
using Identity.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
    /// <summary>
    /// E5 review M2: false when the stored password was generated for the account (Google registration before this
    /// change, administrator provisioning) and the owner never chose it. A password sign-in, reset or change sets it.
    /// </summary>
    public bool PasswordKnownToOwner { get; private set; } = true;

    public bool EmailConfirmed { get; private set; }
    public string? EmailVerificationToken { get; private set; }
    public DateTime? EmailVerificationTokenExpiresAt { get; private set; }

    public string? PasswordResetToken { get; private set; }
    public DateTime? PasswordResetTokenExpiresAt { get; private set; }

    /// <summary>
    /// E5 (#22): an e-mail change waiting for the link sent to the new address. <see cref="Email"/> changes only when
    /// that link is confirmed. Only a SHA-256 digest of the link value is kept (<see cref="PendingEmailToken"/>).
    /// </summary>
    public string? PendingEmail { get; private set; }
    public string? PendingEmailToken { get; private set; }
    public DateTime? PendingEmailExpiresAt { get; private set; }
    /// <summary>When the last change link was sent (one request a minute).</summary>
    public DateTime? PendingEmailRequestedAt { get; private set; }

    public static readonly TimeSpan EmailChangeLinkLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan EmailChangeRequestInterval = TimeSpan.FromMinutes(1);

    /// <summary>Accounts made with Google sign-in have no password until the owner sets one through a reset link.</summary>
    [NotMapped]
    public bool HasPassword => PasswordKnownToOwner && PasswordHash.Length > 0 && PasswordSalt.Length > 0;

    public string? PhoneNumber { get; private set; }
    public bool PhoneConfirmed { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? LastLoginAt { get; private set; }

    public bool MfaEnabled { get; private set; }
    public string? MfaSecretProtected { get; private set; }
    public string MfaRecoveryCodeHashesJson { get; private set; } = "[]";
    public DateTimeOffset? MfaEnabledAt { get; private set; }
    public long? LastAcceptedMfaTimeStep { get; private set; }
    public int MfaFailedAttempts { get; private set; }
    public DateTimeOffset? MfaLockedUntil { get; private set; }

    [NotMapped]
    public IReadOnlyList<string> MfaRecoveryCodeHashes =>
        JsonSerializer.Deserialize<string[]>(MfaRecoveryCodeHashesJson) ?? [];

    // Navigation properties
    private readonly List<UserRole> _roles = new();
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    private readonly List<UserProductAccess> _productAccesses = new();
    public IReadOnlyCollection<UserProductAccess> ProductAccesses => _productAccesses.AsReadOnly();
    
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
        Email = NormalizeEmail(email ?? throw new ArgumentNullException(nameof(email)));
        FirstName = firstName;
        LastName = lastName;
    }

    public static User Create(Guid id, string email, string firstName = "", string lastName = "")
    {
        var user = new User(id, email, firstName, lastName);
        // Domain event can be raised
        return user;
    }

    /// <summary>
    /// Canonical form of an address: trimmed and lower-case (invariant). Stored this way so
    /// the unique index on <see cref="Email"/> is case-insensitive in practice and look-ups
    /// can use it.
    /// </summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>"ogrenci@example.test" → "o***@example.test" (E5: what an e-mail change link may show).</summary>
    public static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }

    /// <param name="knownToOwner">False for a generated password the owner never sees (see <see cref="PasswordKnownToOwner"/>).</param>
    public void SetPassword(byte[] passwordHash, byte[] passwordSalt, bool knownToOwner = true)
    {
        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
        PasswordKnownToOwner = knownToOwner;
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
        var refreshToken = _refreshTokens.SingleOrDefault(t => t.Matches(token));
        if (refreshToken != null && refreshToken.IsActive)
        {
            refreshToken.Revoke(ipAddress, reason);
        }
    }

    /// <summary>The owner entered the stored password (sign-in, re-authentication), so it is theirs.</summary>
    public void MarkPasswordKnownToOwner()
    {
        if (PasswordKnownToOwner || PasswordHash.Length == 0) return;
        PasswordKnownToOwner = true;
        UpdatedAt = DateTime.UtcNow;
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

    /// <summary>
    /// A verified external identity proves ownership of an address that this local
    /// account never confirmed. Whoever registered the account could have chosen its
    /// password, so every credential set before the proof is discarded: the password,
    /// pending reset/verification links and any session. The owner signs in with the
    /// external provider and can set a password through the reset flow.
    /// Paket A review #3: services joined with that password while the address was unconfirmed
    /// (self-granted access: <see cref="UserProductAccessSource.SelfRegistration"/> granted by the
    /// account itself) are removed with their roles; the products are returned so the caller can
    /// also remove the institution links made by those joins. The original registration is kept.
    /// </summary>
    public IReadOnlyList<PlatformProduct> ClaimUnconfirmedAccountByVerifiedExternalLogin()
    {
        if (EmailConfirmed)
        {
            return [];
        }

        var joinedWhileUnconfirmed = _productAccesses
            .Where(access => access.IsActive
                && access.Source == UserProductAccessSource.SelfRegistration
                && access.GrantedByUserId == Id)
            .Select(access => access.Product)
            .ToList();
        // Second review M1: removed (not revoked) so the owner can join that service again.
        _productAccesses.RemoveAll(access => joinedWhileUnconfirmed.Contains(access.Product));
        foreach (var product in joinedWhileUnconfirmed)
            _roles.RemoveAll(role => role.Product == product);

        PasswordHash = [];
        PasswordSalt = [];
        PasswordKnownToOwner = false;
        PasswordResetToken = null;
        PasswordResetTokenExpiresAt = null;
        foreach (var refreshToken in _refreshTokens.Where(token => token.IsActive))
        {
            refreshToken.Revoke(null, "unconfirmed account claimed by verified external login");
        }

        ConfirmEmail();
        return joinedWhileUnconfirmed;
    }

    public void RevokeEmailConfirmation()
    {
        EmailConfirmed = false;
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

    /// <summary>
    /// Issues a single-use reset / password-setup link value and returns it. Only its
    /// SHA-256 digest is kept (<see cref="PasswordResetToken"/>), so a database copy
    /// cannot be used to take over accounts with a pending link.
    /// </summary>
    public string GeneratePasswordResetToken()
    {
        Span<byte> tokenBytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(tokenBytes);
        var token = Convert.ToBase64String(tokenBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        PasswordResetToken = RefreshToken.Digest(token);
        PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(2); // Short lived
        UpdatedAt = DateTime.UtcNow;
        return token;
    }

    /// <summary>Constant-time check of a presented reset link value against the stored digest.</summary>
    public bool PasswordResetTokenMatches(string? presented) =>
        PasswordResetToken is { } stored
        && !string.IsNullOrEmpty(presented)
        && CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(RefreshToken.Digest(presented)),
            System.Text.Encoding.ASCII.GetBytes(stored));

    public void ClearPasswordResetToken()
    {
        PasswordResetToken = null;
        PasswordResetTokenExpiresAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>True when a link was sent less than <see cref="EmailChangeRequestInterval"/> before <paramref name="now"/>.</summary>
    public bool EmailChangeRequestedRecently(DateTime now) =>
        PendingEmailRequestedAt is { } requested && now - requested < EmailChangeRequestInterval;

    /// <summary>
    /// E5 (#22): starts a change to <paramref name="newEmail"/> (normalized) and returns the one-time link value. A
    /// previous pending change is replaced, so only the newest link works.
    /// </summary>
    public string RequestEmailChange(string newEmail, DateTime now)
    {
        Span<byte> tokenBytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(tokenBytes);
        var token = Convert.ToBase64String(tokenBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        PendingEmail = NormalizeEmail(newEmail);
        PendingEmailToken = RefreshToken.Digest(token);
        PendingEmailExpiresAt = now.Add(EmailChangeLinkLifetime);
        PendingEmailRequestedAt = now;
        UpdatedAt = now;
        return token;
    }

    /// <summary>Constant-time check of a presented change link against the stored digest; false when none is pending or it expired.</summary>
    public bool EmailChangeTokenMatches(string? presented, DateTime? now = null) =>
        PendingEmail is not null
        && PendingEmailToken is { } stored
        && PendingEmailExpiresAt > (now ?? DateTime.UtcNow)
        && !string.IsNullOrEmpty(presented)
        && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(RefreshToken.Digest(presented)),
            Encoding.ASCII.GetBytes(stored));

    /// <summary>
    /// Applies the pending change and returns the previous address. The new address is verified by the link, and
    /// links sent to the old address (password reset, verification) stop working.
    /// </summary>
    public string ConfirmEmailChange()
    {
        var pending = PendingEmail ?? throw new InvalidOperationException("No e-mail change is pending.");
        var previous = Email;
        Email = pending;
        ClearPendingEmail();
        PasswordResetToken = null;
        PasswordResetTokenExpiresAt = null;
        ConfirmEmail();
        return previous;
    }

    public void CancelEmailChange()
    {
        ClearPendingEmail();
        UpdatedAt = DateTime.UtcNow;
    }

    private void ClearPendingEmail()
    {
        PendingEmail = null;
        PendingEmailToken = null;
        PendingEmailExpiresAt = null;
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

    public void EnableMfa(
        string protectedSecret,
        IEnumerable<string> recoveryCodeHashes,
        DateTimeOffset enabledAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);
        ArgumentNullException.ThrowIfNull(recoveryCodeHashes);

        var hashes = recoveryCodeHashes
            .Where(hash => !string.IsNullOrWhiteSpace(hash))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (hashes.Length == 0)
        {
            throw new ArgumentException("At least one recovery code hash is required.", nameof(recoveryCodeHashes));
        }

        MfaSecretProtected = protectedSecret;
        MfaRecoveryCodeHashesJson = JsonSerializer.Serialize(hashes);
        MfaEnabledAt = enabledAt;
        MfaEnabled = true;
        LastAcceptedMfaTimeStep = null;
        ResetMfaFailures();
        UpdatedAt = DateTime.UtcNow;
    }

    public bool TryAcceptMfaTimeStep(long timeStep)
    {
        if (!MfaEnabled || LastAcceptedMfaTimeStep >= timeStep)
        {
            return false;
        }

        LastAcceptedMfaTimeStep = timeStep;
        ResetMfaFailures();
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public bool ConsumeMfaRecoveryCode(string recoveryCodeHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryCodeHash);

        var hashes = MfaRecoveryCodeHashes.ToList();
        var supplied = Encoding.UTF8.GetBytes(recoveryCodeHash);
        var matchIndex = hashes.FindIndex(stored =>
        {
            var candidate = Encoding.UTF8.GetBytes(stored);
            return candidate.Length == supplied.Length
                && CryptographicOperations.FixedTimeEquals(candidate, supplied);
        });

        if (matchIndex < 0)
        {
            return false;
        }

        hashes.RemoveAt(matchIndex);
        MfaRecoveryCodeHashesJson = JsonSerializer.Serialize(hashes);
        ResetMfaFailures();
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public void RecordFailedMfaAttempt(DateTimeOffset now)
    {
        if (MfaLockedUntil <= now)
        {
            MfaFailedAttempts = 0;
            MfaLockedUntil = null;
        }

        MfaFailedAttempts++;
        if (MfaFailedAttempts >= 5)
        {
            MfaLockedUntil = now.AddMinutes(5);
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsMfaVerificationLocked(DateTimeOffset now) => MfaLockedUntil > now;

    public void ResetMfaFailures()
    {
        MfaFailedAttempts = 0;
        MfaLockedUntil = null;
    }

    public void ResetMfa()
    {
        MfaEnabled = false;
        MfaSecretProtected = null;
        MfaRecoveryCodeHashesJson = "[]";
        MfaEnabledAt = null;
        LastAcceptedMfaTimeStep = null;
        ResetMfaFailures();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        foreach (var refreshToken in _refreshTokens.Where(token => token.IsActive))
        {
            refreshToken.Revoke(null, "security-sensitive account deactivation");
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void AnonymizeForErasure()
    {
        Email = $"erased-{Id:N}@deleted.invalid";
        FirstName = string.Empty;
        LastName = string.Empty;
        PhoneNumber = null;
        PhoneConfirmed = false;
        PasswordHash = [];
        PasswordSalt = [];
        PasswordKnownToOwner = false;
        EmailConfirmed = false;
        EmailVerificationToken = null;
        EmailVerificationTokenExpiresAt = null;
        PasswordResetToken = null;
        PasswordResetTokenExpiresAt = null;
        ClearPendingEmail();
        PendingEmailRequestedAt = null;
        LastLoginAt = null;
        ResetMfa();
        Deactivate();
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddRole(UserRole userRole)
    {
        if (!_roles.Any(r => r.RoleId == userRole.RoleId && r.Product == userRole.Product))
        {
            _roles.Add(userRole);
        }
    }

    public bool HasProductAccess(PlatformProduct product) =>
        _productAccesses.Any(access => access.Product == product && access.IsActive);

    public IEnumerable<UserRole> GetRolesForProductScope(PlatformProduct? product) => product is null
        ? _roles.Where(userRole => userRole.Product is null)
        : _roles.Where(userRole => userRole.Product == product
            || userRole.Product is null
            && string.Equals(
                userRole.Role?.Name,
                Identity.Domain.Enums.UserRole.SystemAdmin.ToString(),
                StringComparison.OrdinalIgnoreCase));

    public bool GrantProductAccess(
        PlatformProduct product,
        UserProductAccessSource source,
        Guid? grantedByUserId,
        DateTimeOffset grantedAt)
    {
        var access = _productAccesses.FirstOrDefault(candidate => candidate.Product == product);
        if (access is null)
        {
            _productAccesses.Add(UserProductAccess.Create(Id, product, source, grantedByUserId, grantedAt));
            return true;
        }

        if (!access.IsActive)
            access.Reactivate(source, grantedByUserId, grantedAt);

        return false;
    }

    public void RevokeProductAccess(PlatformProduct product, DateTimeOffset revokedAt)
    {
        _productAccesses.FirstOrDefault(access => access.Product == product)?.Revoke(revokedAt);
    }

    public void RemoveRole(Guid roleId, PlatformProduct? product = null)
    {
        var role = _roles.FirstOrDefault(r => r.RoleId == roleId && r.Product == product);
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
    public PlatformProduct? Product { get; private set; }

    private UserRole() { }

    public UserRole(Guid userId, Guid roleId, PlatformProduct? product = null)
    {
        if (product.HasValue && !Enum.IsDefined(product.Value))
            throw new ArgumentOutOfRangeException(nameof(product));

        UserId = userId;
        RoleId = roleId;
        Product = product;
    }
}
