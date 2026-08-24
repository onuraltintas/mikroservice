namespace SpeedReading.Infrastructure.Legacy;

internal abstract class LegacyBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

internal sealed class LegacyExerciseTypeCategory : LegacyBaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

internal sealed class LegacyExerciseType : LegacyBaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconName { get; set; } = string.Empty;
    public string ColorCode { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public string EngineType { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
}

internal sealed class LegacyExercise : LegacyBaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DifficultyLevel { get; set; }
    public Guid ExerciseTypeId { get; set; }
    public string ConfigurationJson { get; set; } = string.Empty;
    public Guid? TargetAgeGroupConfigurationId { get; set; }
    public Guid CreatorId { get; set; }
}

internal sealed class LegacyReadingText : LegacyBaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int WordCount { get; set; }
    public string Category { get; set; } = string.Empty;
    public int DifficultyLevel { get; set; }
    public Guid? TargetAgeGroupConfigurationId { get; set; }
    public string Language { get; set; } = "tr";
    public bool IsActive { get; set; }
    public string Tags { get; set; } = string.Empty;
    public int RecommendedMinLevel { get; set; }
    public int RecommendedMaxLevel { get; set; }
    public decimal AverageComprehensionScore { get; set; }
    public int TimesRead { get; set; }
    public int AverageReadingTimeSeconds { get; set; }
    public Guid? ExerciseId { get; set; }
}

internal sealed class LegacyReadingQuestion : LegacyBaseEntity
{
    public Guid ReadingTextId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int Type { get; set; }
    public int BloomLevel { get; set; }
    public int DifficultyLevel { get; set; }
    public string? Explanation { get; set; }
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}
