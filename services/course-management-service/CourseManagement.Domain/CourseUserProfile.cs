namespace CourseManagement.Domain;

public sealed class CourseUserProfile
{
    private CourseUserProfile() { }

    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }

    public static CourseUserProfile Create(Guid userId, string displayName)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User identifier is required.", nameof(userId));
        var profile = new CourseUserProfile { UserId = userId };
        profile.UpdateDisplayName(displayName);
        return profile;
    }

    public void UpdateDisplayName(string displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        var normalizedName = displayName.Trim();
        if (normalizedName.Length is < 1 or > 200)
            throw new ArgumentException("Display name must contain between 1 and 200 characters.", nameof(displayName));
        DisplayName = normalizedName;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
