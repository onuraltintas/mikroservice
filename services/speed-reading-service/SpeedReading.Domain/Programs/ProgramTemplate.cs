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
    public int DisplayOrder { get; private set; }
    public int ProgramType { get; private set; }
    public string? ExamType { get; private set; }
    public bool IsAssessment { get; private set; }

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
        if (totalWeeks < 0 || totalDays < 0 || minAssessmentScore < 0 || maxAssessmentScore < 0)
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
}
