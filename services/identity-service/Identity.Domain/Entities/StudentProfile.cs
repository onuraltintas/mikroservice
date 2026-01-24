using EduPlatform.Shared.Kernel.Primitives;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

/// <summary>
/// Öğrenci Profili
/// </summary>
public class StudentProfile : AggregateRoot
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;

    public Guid? InstitutionId { get; private set; }
    public Institution? Institution { get; private set; }

    public Guid? ParentId { get; private set; }
    public User? Parent { get; private set; }

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public DateTime? BirthDate { get; private set; }
    public Gender? Gender { get; private set; }

    public int? GradeLevel { get; private set; } // 1-12
    public string? SchoolName { get; private set; }
    public string? SchoolCity { get; private set; }

    public string? AvatarUrl { get; private set; }
    public string? Bio { get; private set; }
    public LearningStyle? LearningStyle { get; private set; }
    public int DailyGoalMinutes { get; private set; } = 30;

    public string Preferences { get; private set; } = "{}"; // JSON

    public bool IsActive { get; private set; } = true;

    // Navigation - Öğretmen atamaları
    private readonly List<TeacherStudentAssignment> _teacherAssignments = new();
    public IReadOnlyCollection<TeacherStudentAssignment> TeacherAssignments => _teacherAssignments.AsReadOnly();

    private StudentProfile() { }

    public static StudentProfile Create(
        Guid userId,
        string firstName,
        string lastName,
        Guid? institutionId = null,
        Guid? parentId = null)
    {
        return new StudentProfile
        {
            UserId = userId,
            FirstName = firstName ?? throw new ArgumentNullException(nameof(firstName)),
            LastName = lastName ?? throw new ArgumentNullException(nameof(lastName)),
            InstitutionId = institutionId,
            ParentId = parentId
        };
    }

    public void UpdatePersonalInfo(
        string? firstName = null,
        string? lastName = null,
        DateTime? birthDate = null,
        Gender? gender = null)
    {
        if (firstName != null) FirstName = firstName;
        if (lastName != null) LastName = lastName;
        if (birthDate.HasValue) BirthDate = birthDate;
        if (gender.HasValue) Gender = gender;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateEducationInfo(
        int? gradeLevel = null,
        string? schoolName = null,
        string? schoolCity = null)
    {
        if (gradeLevel.HasValue)
        {
            if (gradeLevel < 1 || gradeLevel > 12)
                throw new ArgumentOutOfRangeException(nameof(gradeLevel), "Grade must be between 1 and 12");
            GradeLevel = gradeLevel;
        }
        if (schoolName != null) SchoolName = schoolName;
        if (schoolCity != null) SchoolCity = schoolCity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetLearningPreferences(LearningStyle? style = null, int? dailyGoalMinutes = null)
    {
        if (style.HasValue) LearningStyle = style;
        if (dailyGoalMinutes.HasValue)
        {
            if (dailyGoalMinutes < 5 || dailyGoalMinutes > 480)
                throw new ArgumentOutOfRangeException(nameof(dailyGoalMinutes), "Daily goal must be between 5 and 480 minutes");
            DailyGoalMinutes = dailyGoalMinutes.Value;
        }
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
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveFromInstitution()
    {
        InstitutionId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignParent(Guid parentId)
    {
        ParentId = parentId;
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

    public string FullName => $"{FirstName} {LastName}";
}
