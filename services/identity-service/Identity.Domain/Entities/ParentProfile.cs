using EduPlatform.Shared.Kernel.Primitives;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

/// <summary>
/// Veli Profili
/// </summary>
public class ParentProfile : AggregateRoot
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public ParentRelationship? Relationship { get; private set; }

    public string NotificationPreferences { get; private set; } = @"{
        ""dailyReport"": false,
        ""weeklyProgress"": true,
        ""examResults"": true,
        ""lowActivityAlert"": true
    }"; // JSON

    public bool IsActive { get; private set; } = true;

    // Navigation - Çocuklar
    private readonly List<StudentProfile> _children = new();
    public IReadOnlyCollection<StudentProfile> Children => _children.AsReadOnly();

    private ParentProfile() { }

    public static ParentProfile Create(
        Guid userId,
        string firstName,
        string lastName,
        ParentRelationship? relationship = null)
    {
        return new ParentProfile
        {
            UserId = userId,
            FirstName = firstName ?? throw new ArgumentNullException(nameof(firstName)),
            LastName = lastName ?? throw new ArgumentNullException(nameof(lastName)),
            Relationship = relationship
        };
    }

    public void UpdatePersonalInfo(
        string? firstName = null,
        string? lastName = null,
        string? phoneNumber = null,
        ParentRelationship? relationship = null)
    {
        if (firstName != null) FirstName = firstName;
        if (lastName != null) LastName = lastName;
        if (phoneNumber != null) PhoneNumber = phoneNumber;
        if (relationship.HasValue) Relationship = relationship;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateNotificationPreferences(string preferencesJson)
    {
        NotificationPreferences = preferencesJson;
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
