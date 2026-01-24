using EduPlatform.Shared.Kernel.Primitives;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

/// <summary>
/// Kurum entity - Okul, Dershane, Etüt Merkezi, Online Platform
/// </summary>
public class Institution : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public InstitutionType Type { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public string? District { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Website { get; private set; }
    public string? TaxNumber { get; private set; }

    // Lisans Bilgileri
    public LicenseType LicenseType { get; private set; } = LicenseType.Trial;
    public int MaxStudents { get; private set; } = 50;
    public int MaxTeachers { get; private set; } = 5;
    public DateTime? SubscriptionStartDate { get; private set; }
    public DateTime? SubscriptionEndDate { get; private set; }

    public bool IsActive { get; private set; } = true;

    // Navigation properties
    private readonly List<InstitutionAdmin> _admins = new();
    public IReadOnlyCollection<InstitutionAdmin> Admins => _admins.AsReadOnly();

    private readonly List<TeacherProfile> _teachers = new();
    public IReadOnlyCollection<TeacherProfile> Teachers => _teachers.AsReadOnly();

    private readonly List<StudentProfile> _students = new();
    public IReadOnlyCollection<StudentProfile> Students => _students.AsReadOnly();

    private Institution() { }

    public static Institution Create(
        string name,
        InstitutionType type,
        string? city = null,
        string? email = null)
    {
        var institution = new Institution
        {
            Name = name ?? throw new ArgumentNullException(nameof(name)),
            Type = type,
            City = city,
            Email = email,
            SubscriptionStartDate = DateTime.UtcNow,
            SubscriptionEndDate = DateTime.UtcNow.AddDays(14) // Trial
        };

        // Set default limits based on type
        institution.SetDefaultLimits();

        return institution;
    }

    private void SetDefaultLimits()
    {
        (MaxStudents, MaxTeachers) = Type switch
        {
            InstitutionType.School => (500, 50),
            InstitutionType.PrivateCourse => (200, 20),
            InstitutionType.StudyCenter => (50, 5),
            InstitutionType.OnlinePlatform => (1000, 10),
            _ => (50, 5)
        };
    }

    public void UpdateInfo(
        string? name = null,
        string? address = null,
        string? city = null,
        string? district = null,
        string? phone = null,
        string? email = null,
        string? website = null)
    {
        if (name != null) Name = name;
        if (address != null) Address = address;
        if (city != null) City = city;
        if (district != null) District = district;
        if (phone != null) Phone = phone;
        if (email != null) Email = email;
        if (website != null) Website = website;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetLogo(string logoUrl)
    {
        LogoUrl = logoUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpgradeLicense(LicenseType licenseType, int maxStudents, int maxTeachers, DateTime endDate)
    {
        LicenseType = licenseType;
        MaxStudents = maxStudents;
        MaxTeachers = maxTeachers;
        SubscriptionEndDate = endDate;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool CanAddStudent() => _students.Count(s => s.IsActive) < MaxStudents;
    public bool CanAddTeacher() => _teachers.Count(t => t.IsActive) < MaxTeachers;
    public bool IsSubscriptionActive() => SubscriptionEndDate == null || SubscriptionEndDate > DateTime.UtcNow;

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
}

/// <summary>
/// Kurum Yöneticisi
/// </summary>
public class InstitutionAdmin : Entity
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    
    public Guid InstitutionId { get; private set; }
    public Institution Institution { get; private set; } = null!;
    
    public InstitutionAdminRole Role { get; private set; }
    public string? Permissions { get; private set; } // JSON
    public bool IsActive { get; private set; } = true;

    private InstitutionAdmin() { }

    public static InstitutionAdmin Create(Guid userId, Guid institutionId, InstitutionAdminRole role)
    {
        return new InstitutionAdmin
        {
            UserId = userId,
            InstitutionId = institutionId,
            Role = role
        };
    }

    public void ChangeRole(InstitutionAdminRole newRole)
    {
        Role = newRole;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
