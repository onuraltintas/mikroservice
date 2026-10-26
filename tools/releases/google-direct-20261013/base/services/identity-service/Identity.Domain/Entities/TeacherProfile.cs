using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Domain.Entities;

/// <summary>
/// Öğretmen Profili
/// </summary>
public class TeacherProfile : AggregateRoot
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;

    public Guid? InstitutionId { get; private set; }
    public Institution? Institution { get; private set; }

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? Title { get; private set; } // Öğretmen, Uzman, Prof. Dr.

    private readonly List<string> _subjects = new();
    public IReadOnlyList<string> Subjects => _subjects.AsReadOnly();
    
    public int? ExperienceYears { get; private set; }

    public string? AvatarUrl { get; private set; }
    public string? Bio { get; private set; }
    public string Certifications { get; private set; } = "[]"; // JSON

    public bool IsIndependent { get; private set; } // Kurumdan bağımsız mı?
    public bool CanViewAllInstitutionStudents { get; private set; } // Kurumdaki tüm öğrencileri görebilir mi?

    public bool IsActive { get; private set; } = true;

    // Navigation - Öğrenci atamaları
    private readonly List<TeacherStudentAssignment> _studentAssignments = new();
    public IReadOnlyCollection<TeacherStudentAssignment> StudentAssignments => _studentAssignments.AsReadOnly();

    private TeacherProfile() { }

    public static TeacherProfile Create(
        Guid userId,
        string firstName,
        string lastName,
        Guid? institutionId = null,
        bool isIndependent = false)
    {
        return new TeacherProfile
        {
            UserId = userId,
            FirstName = firstName ?? throw new ArgumentNullException(nameof(firstName)),
            LastName = lastName ?? throw new ArgumentNullException(nameof(lastName)),
            InstitutionId = institutionId,
            IsIndependent = isIndependent || institutionId == null
        };
    }

    public void UpdatePersonalInfo(
        string? firstName = null,
        string? lastName = null,
        string? title = null,
        int? experienceYears = null)
    {
        if (firstName != null) FirstName = firstName;
        if (lastName != null) LastName = lastName;
        if (title != null) Title = title;
        if (experienceYears.HasValue) ExperienceYears = experienceYears;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetSubjects(IEnumerable<string> subjects)
    {
        _subjects.Clear();
        _subjects.AddRange(subjects);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddSubject(string subject)
    {
        if (!_subjects.Contains(subject))
        {
            _subjects.Add(subject);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void RemoveSubject(string subject)
    {
        _subjects.Remove(subject);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAvatar(string avatarUrl)
    {
        AvatarUrl = avatarUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetBio(string bio)
    {
        Bio = bio;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignToInstitution(Guid institutionId)
    {
        InstitutionId = institutionId;
        IsIndependent = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveFromInstitution()
    {
        InstitutionId = null;
        IsIndependent = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetViewAllStudentsPermission(bool canView)
    {
        CanViewAllInstitutionStudents = canView;
        UpdatedAt = DateTime.UtcNow;
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

    public string FullName => $"{Title} {FirstName} {LastName}".Trim();
}
