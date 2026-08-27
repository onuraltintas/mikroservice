using System.Text.Json;
using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Programs;

public sealed class ProgramTemplate : AggregateRoot
{
    private ProgramTemplate()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid TargetAgeGroupConfigurationId { get; private set; }
    public int MinAssessmentScore { get; private set; }
    public int MaxAssessmentScore { get; private set; }
    public string WeeklyPatternJson { get; private set; } = "{}";
    public int InitialDifficultyLevel { get; private set; }
    public int WeeksPerDifficultyIncrease { get; private set; }
    public int MaxDifficultyLevel { get; private set; }
    public int TotalWeeks { get; private set; }
    public int TotalDays { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }
    public int DisplayOrder { get; private set; }
    public int ProgramType { get; private set; }
    public string? ExamType { get; private set; }
    public bool IsAssessment { get; private set; }

    public static ProgramTemplate Create(
        string name,
        string description,
        Guid targetAgeGroupConfigurationId,
        int minAssessmentScore,
        int maxAssessmentScore,
        string weeklyPatternJson,
        int initialDifficultyLevel,
        int weeksPerDifficultyIncrease,
        int maxDifficultyLevel,
        int totalWeeks,
        int totalDays,
        bool isActive,
        int displayOrder,
        int programType,
        string? examType,
        bool isAssessment,
        Guid actorId,
        DateTime createdAt) 
    {
        ValidateEditable(
            name,
            description,
            targetAgeGroupConfigurationId,
            minAssessmentScore,
            maxAssessmentScore,
            weeklyPatternJson,
            initialDifficultyLevel,
            weeksPerDifficultyIncrease,
            maxDifficultyLevel,
            totalWeeks,
            totalDays,
            displayOrder,
            programType,
            examType,
            actorId);

        return Import(
            Guid.NewGuid(),
            name,
            description,
            targetAgeGroupConfigurationId,
            minAssessmentScore,
            maxAssessmentScore,
            weeklyPatternJson,
            initialDifficultyLevel,
            weeksPerDifficultyIncrease,
            maxDifficultyLevel,
            totalWeeks,
            totalDays,
            isActive,
            displayOrder,
            programType,
            examType,
            isAssessment,
            createdAt,
            actorId.ToString(),
            null,
            null);
    }

    public void Update(
        string name,
        string description,
        Guid targetAgeGroupConfigurationId,
        int minAssessmentScore,
        int maxAssessmentScore,
        string weeklyPatternJson,
        int initialDifficultyLevel,
        int weeksPerDifficultyIncrease,
        int maxDifficultyLevel,
        int totalWeeks,
        int totalDays,
        bool isActive,
        int displayOrder,
        int programType,
        string? examType,
        bool isAssessment,
        Guid actorId,
        DateTime updatedAt)
    {
        ValidateEditable(
            name,
            description,
            targetAgeGroupConfigurationId,
            minAssessmentScore,
            maxAssessmentScore,
            weeklyPatternJson,
            initialDifficultyLevel,
            weeksPerDifficultyIncrease,
            maxDifficultyLevel,
            totalWeeks,
            totalDays,
            displayOrder,
            programType,
            examType,
            actorId);

        Name = name.Trim();
        Description = description.Trim();
        TargetAgeGroupConfigurationId = targetAgeGroupConfigurationId;
        MinAssessmentScore = minAssessmentScore;
        MaxAssessmentScore = maxAssessmentScore;
        WeeklyPatternJson = weeklyPatternJson;
        InitialDifficultyLevel = initialDifficultyLevel;
        WeeksPerDifficultyIncrease = weeksPerDifficultyIncrease;
        MaxDifficultyLevel = maxDifficultyLevel;
        TotalWeeks = totalWeeks;
        TotalDays = totalDays;
        IsActive = isActive;
        DisplayOrder = displayOrder;
        ProgramType = programType;
        ExamType = examType?.Trim();
        IsAssessment = isAssessment;
        UpdatedAt = EnsureUtc(updatedAt);
        UpdatedBy = actorId.ToString();
    }

    public ProgramTemplate Clone(Guid id, Guid actorId, DateTime createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Cloned program template identifier is required.", nameof(id));
        if (actorId == Guid.Empty)
            throw new ArgumentException("Clone actor is required.", nameof(actorId));

        var clonedName = $"{Name} - Kopya";
        if (clonedName.Length > 200)
            clonedName = clonedName[..200];

        return Import(
            id,
            clonedName,
            Description,
            TargetAgeGroupConfigurationId,
            MinAssessmentScore,
            MaxAssessmentScore,
            WeeklyPatternJson,
            InitialDifficultyLevel,
            WeeksPerDifficultyIncrease,
            MaxDifficultyLevel,
            TotalWeeks,
            TotalDays,
            isActive: false,
            displayOrder: DisplayOrder + 1,
            programType: ProgramType,
            examType: ExamType,
            isAssessment: IsAssessment,
            createdAt: createdAt,
            createdBy: actorId.ToString(),
            updatedAt: null,
            updatedBy: null);
    }

    public void Delete(Guid actorId, DateTime deletedAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Delete actor is required.", nameof(actorId));

        IsDeleted = true;
        IsActive = false;
        DeletedAt = EnsureUtc(deletedAt);
        DeletedBy = actorId.ToString();
        UpdatedAt = DeletedAt;
        UpdatedBy = DeletedBy;
    }

    public static ProgramTemplate Import(
        Guid id,
        string name,
        string description,
        Guid targetAgeGroupConfigurationId,
        int minAssessmentScore,
        int maxAssessmentScore,
        string weeklyPatternJson,
        int initialDifficultyLevel,
        int weeksPerDifficultyIncrease,
        int maxDifficultyLevel,
        int totalWeeks,
        int totalDays,
        bool isActive,
        int displayOrder,
        int programType,
        string? examType,
        bool isAssessment,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Program template identifier is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Program template name is required.", nameof(name));
        if (totalWeeks < 0 || totalDays < 0 || minAssessmentScore < 0 || maxAssessmentScore < 0
            || maxAssessmentScore < minAssessmentScore)
            throw new ArgumentOutOfRangeException(nameof(totalDays));

        return new ProgramTemplate
        {
            Id = id,
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            TargetAgeGroupConfigurationId = targetAgeGroupConfigurationId,
            MinAssessmentScore = minAssessmentScore,
            MaxAssessmentScore = maxAssessmentScore,
            WeeklyPatternJson = string.IsNullOrWhiteSpace(weeklyPatternJson) ? "{}" : weeklyPatternJson,
            InitialDifficultyLevel = initialDifficultyLevel,
            WeeksPerDifficultyIncrease = weeksPerDifficultyIncrease,
            MaxDifficultyLevel = maxDifficultyLevel,
            TotalWeeks = totalWeeks,
            TotalDays = totalDays,
            IsActive = isActive,
            IsDeleted = false,
            DisplayOrder = displayOrder,
            ProgramType = programType,
            ExamType = examType,
            IsAssessment = isAssessment,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy,
            UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null,
            UpdatedBy = updatedBy
        };
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static void ValidateEditable(
        string name,
        string description,
        Guid targetAgeGroupConfigurationId,
        int minAssessmentScore,
        int maxAssessmentScore,
        string weeklyPatternJson,
        int initialDifficultyLevel,
        int weeksPerDifficultyIncrease,
        int maxDifficultyLevel,
        int totalWeeks,
        int totalDays,
        int displayOrder,
        int programType,
        string? examType,
        Guid actorId)
    {
        if (actorId == Guid.Empty
            || targetAgeGroupConfigurationId == Guid.Empty
            || string.IsNullOrWhiteSpace(name)
            || name.Trim().Length > 200
            || (description?.Length ?? 0) > 5_000
            || minAssessmentScore is < 0 or > 100
            || maxAssessmentScore is < 0 or > 100
            || maxAssessmentScore < minAssessmentScore
            || string.IsNullOrWhiteSpace(weeklyPatternJson)
            || weeklyPatternJson.Length > 1_048_576
            || initialDifficultyLevel is < 0 or > 10
            || weeksPerDifficultyIncrease is < 1 or > 52
            || maxDifficultyLevel is < 0 or > 10
            || maxDifficultyLevel < initialDifficultyLevel
            || totalWeeks is < 1 or > 520
            || totalDays is < 1 or > 3_650
            || displayOrder is < 0 or > 10_000
            || programType is < 0 or > 100
            || (examType?.Length ?? 0) > 100)
        {
            throw new ArgumentException("Program template fields are invalid.", nameof(name));
        }

        try
        {
            using var document = JsonDocument.Parse(weeklyPatternJson);
            if (document.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                throw new ArgumentException("WeeklyPatternJson must be an object or array.", nameof(weeklyPatternJson));
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("WeeklyPatternJson must be valid JSON.", nameof(weeklyPatternJson), exception);
        }
    }
}
