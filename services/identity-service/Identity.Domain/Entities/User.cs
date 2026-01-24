using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Domain.Entities;

/// <summary>
/// Kullanıcı entity - Keycloak ile senkronize
/// </summary>
public class User : AggregateRoot
{
    public string Email { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;

    public bool EmailConfirmed { get; private set; }
    public string? PhoneNumber { get; private set; }
    public bool PhoneConfirmed { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? LastLoginAt { get; private set; }

    // Navigation properties
    private readonly List<UserRole> _roles = new();
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();
    
    public StudentProfile? StudentProfile { get; private set; }
    public TeacherProfile? TeacherProfile { get; private set; }
    public ParentProfile? ParentProfile { get; private set; }

    private User() { } // EF Core için

    public User(Guid id, string email, string firstName, string lastName) : base(id)
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
        FirstName = firstName;
        LastName = lastName;
    }

    public static User Create(Guid keycloakUserId, string email, string firstName = "", string lastName = "")
    {
        var user = new User(keycloakUserId, email, firstName, lastName);
        // Domain event raise edilebilir
        return user;
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

    public void AddRole(UserRole role)
    {
        if (!_roles.Any(r => r.Role == role.Role))
        {
            _roles.Add(role);
        }
    }

    public void RemoveRole(Enums.UserRole roleType)
    {
        var role = _roles.FirstOrDefault(r => r.Role == roleType);
        if (role != null)
        {
            _roles.Remove(role);
        }
    }
}

/// <summary>
/// Kullanıcı rolü
/// </summary>
public class UserRole : Entity
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Enums.UserRole Role { get; private set; }

    private UserRole() { }

    public UserRole(Guid userId, Enums.UserRole role)
    {
        UserId = userId;
        Role = role;
    }
}
