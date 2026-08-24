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

internal sealed class LegacyExerciseSession : LegacyBaseEntity
{
    public Guid StudentId { get; set; }
    public Guid ExerciseId { get; set; }
    public int Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int TotalPausedSeconds { get; set; }
    public string SessionDataJson { get; set; } = string.Empty;
    public string? CustomDataJson { get; set; }
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public Guid? ReadingTextId { get; set; }
    public Guid? StudentAssignmentId { get; set; }
}

internal sealed class LegacyStudentExerciseResult : LegacyBaseEntity
{
    public Guid StudentId { get; set; }
    public Guid ExerciseId { get; set; }
    public Guid? ReadingTextId { get; set; }
    public int WordsRead { get; set; }
    public int TimeSpentSeconds { get; set; }
    public decimal RawWPM { get; set; }
    public decimal ComprehensionScore { get; set; }
    public decimal WeightedKDP { get; set; }
    public string QuestionAnswersJson { get; set; } = "[]";
    public string ReadingMovementsJson { get; set; } = "[]";
    public DateTime CompletedAt { get; set; }
}

internal sealed class LegacyReadingSession : LegacyBaseEntity
{
    public Guid UserId { get; set; }
    public Guid ReadingTextId { get; set; }
    public int ReadingTimeSeconds { get; set; }
    public int CalculatedWPM { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalQuestions { get; set; }
    public decimal ComprehensionRate { get; set; }
    public decimal EfficiencyScore { get; set; }
    public DateTime CompletedAt { get; set; }
}

internal sealed class LegacyExerciseProgramTemplate : LegacyBaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid TargetAgeGroupConfigurationId { get; set; }
    public int MinAssessmentScore { get; set; }
    public int MaxAssessmentScore { get; set; }
    public string WeeklyPatternJson { get; set; } = string.Empty;
    public int InitialDifficultyLevel { get; set; }
    public int WeeksPerDifficultyIncrease { get; set; }
    public int MaxDifficultyLevel { get; set; }
    public int TotalWeeks { get; set; }
    public int TotalDays { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    public int ProgramType { get; set; }
    public string? ExamType { get; set; }
    public bool IsAssessment { get; set; }
}

internal sealed class LegacyStudentProgramProgress : LegacyBaseEntity
{
    public Guid UserId { get; set; }
    public Guid ProgramTemplateId { get; set; }
    public DateTime AssignedDate { get; set; }
    public int CurrentDay { get; set; }
    public int CurrentWeek { get; set; }
    public int CurrentDifficultyLevel { get; set; }
    public int DaysCompleted { get; set; }
    public int ExercisesCompleted { get; set; }
    public DateTime? LastCompletionDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CompletedDate { get; set; }
    public decimal AverageSuccessRate { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
}

internal sealed class LegacyDailyExerciseLog : LegacyBaseEntity
{
    public Guid UserId { get; set; }
    public Guid StudentProgramProgressId { get; set; }
    public Guid ExerciseId { get; set; }
    public Guid ExerciseTypeId { get; set; }
    public int DayNumber { get; set; }
    public int WeekNumber { get; set; }
    public int DifficultyLevel { get; set; }
    public DateTime CompletedDate { get; set; }
    public int TimeSpentSeconds { get; set; }
    public decimal SuccessRate { get; set; }
    public bool IsPassed { get; set; }
    public int AttemptNumber { get; set; }
    public bool IsRetry { get; set; }
    public string DevicePlatform { get; set; } = string.Empty;
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public int TotalAttempts { get; set; }
    public decimal? AverageWPM { get; set; }
    public decimal? AverageComprehension { get; set; }
}
