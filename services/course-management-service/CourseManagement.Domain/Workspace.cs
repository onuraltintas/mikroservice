namespace CourseManagement.Domain;

public enum WorkspaceType
{
    Institution = 1,
    IndependentTeacher = 2
}

public sealed class Workspace
{
    private Workspace() { }

    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public WorkspaceType Type { get; private set; }
    public InstitutionCategory? InstitutionCategory { get; private set; }
    public bool IsErasureReadOnly { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    /// <summary>The plan end the owner was last warned about (data deletion a year after it); null when never warned.</summary>
    public DateTimeOffset? RetentionWarningForEndsAt { get; private set; }
    public DateTimeOffset? RetentionWarnedAt { get; private set; }
    /// <summary>When the panel data was last deleted for retention; the workspace and its owner stay.</summary>
    public DateTimeOffset? ContentPurgedAt { get; private set; }
    /// <summary>Setup guide (20.11.2026): the steps the owner chose to skip, comma separated; done steps are read from the data.</summary>
    public string? SetupSkippedSteps { get; private set; }
    /// <summary>When the owner closed the setup guide for good; null while it may still open on its own.</summary>
    public DateTimeOffset? SetupClosedAt { get; private set; }

    public static Workspace Create(Guid id, Guid ownerUserId, string name, WorkspaceType type)
    {
        if (id == Guid.Empty || ownerUserId == Guid.Empty || !Enum.IsDefined(type))
            throw new ArgumentException("Valid workspace identifiers and type are required.");
        ArgumentNullException.ThrowIfNull(name);
        var normalizedName = name.Trim();
        if (normalizedName.Length is < 1 or > 200)
            throw new ArgumentException("Workspace name must contain between 1 and 200 characters.", nameof(name));

        return new Workspace
        {
            Id = id,
            OwnerUserId = ownerUserId,
            Name = normalizedName,
            Type = type,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void ImportInstitutionProfile(string? name, InstitutionCategory? institutionCategory)
    {
        if (Type != WorkspaceType.Institution)
            throw new InvalidOperationException("Only institution workspaces can import institution details.");
        if (name is null && institutionCategory is null)
            throw new ArgumentException("At least one institution detail must be selected.");
        if (name is not null)
        {
            var normalizedName = name.Trim();
            if (normalizedName.Length is < 1 or > 200)
                throw new ArgumentException("Institution name must contain between 1 and 200 characters.", nameof(name));
            Name = normalizedName;
        }
        if (institutionCategory is { } category)
        {
            if (!Enum.IsDefined(category)) throw new ArgumentOutOfRangeException(nameof(institutionCategory));
            InstitutionCategory = category;
        }
    }

    /// <summary>The setup guide's own memory: skipped steps (known step names only) and whether it was closed.</summary>
    public void SetSetupProgress(IEnumerable<string> skippedSteps, bool closed, DateTimeOffset now)
    {
        var steps = skippedSteps.Select(step => step.Trim()).Where(SetupSteps.Contains).Distinct(StringComparer.Ordinal).OrderBy(step => step, StringComparer.Ordinal).ToArray();
        SetupSkippedSteps = steps.Length == 0 ? null : string.Join(',', steps);
        SetupClosedAt = closed ? SetupClosedAt ?? now : null;
    }

    /// <summary>The setup guide's steps, in order.</summary>
    public static readonly IReadOnlyList<string> SetupSteps = ["profile", "teachers", "rooms", "courses", "students", "fees", "schedule", "emails"];

    /// <summary>The owner renames the institution or chooses its kind (setup guide, Ayarlar).</summary>
    public void UpdateProfile(string name, InstitutionCategory? institutionCategory)
    {
        ArgumentNullException.ThrowIfNull(name);
        var normalizedName = name.Trim();
        if (normalizedName.Length is < 1 or > 200)
            throw new ArgumentException("Workspace name must contain between 1 and 200 characters.", nameof(name));
        if (institutionCategory is { } category && !Enum.IsDefined(category)) throw new ArgumentOutOfRangeException(nameof(institutionCategory));
        Name = normalizedName;
        InstitutionCategory = institutionCategory;
    }

    public void MarkRetentionWarned(DateTimeOffset planEndsAt, DateTimeOffset now)
    {
        RetentionWarningForEndsAt = planEndsAt;
        RetentionWarnedAt = now;
    }

    public void MarkContentPurged(DateTimeOffset now)
    {
        ContentPurgedAt = now;
        RetentionWarningForEndsAt = null;
        RetentionWarnedAt = null;
    }

    public void SetErasureReadOnly()
    {
        IsErasureReadOnly = true;
        if (Type == WorkspaceType.IndependentTeacher)
            Name = "Silinmiş çalışma alanı";
    }

    public void TransferOwnership(Guid ownerUserId)
    {
        if (ownerUserId == Guid.Empty) throw new ArgumentException("Workspace owner is required.", nameof(ownerUserId));
        OwnerUserId = ownerUserId;
    }
}
