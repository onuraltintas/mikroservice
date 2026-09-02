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

internal sealed class LegacyContentBlock : LegacyBaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string? Label { get; set; }
    public int Type { get; set; }
    public string Value { get; set; } = string.Empty;
}

internal sealed class LegacyPage : LegacyBaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTime? ScheduledPublishAt { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? CanonicalUrl { get; set; }
    public string? OgTitle { get; set; }
    public string? OgDescription { get; set; }
    public string? OgImage { get; set; }
    public bool SeoSettingsNoIndex { get; set; }
}

internal sealed class LegacyBlogPost : LegacyBaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? Tags { get; set; }
    public string? Author { get; set; }
    public int ViewCount { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ScheduledPublishAt { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? CanonicalUrl { get; set; }
    public string? OgTitle { get; set; }
    public string? OgDescription { get; set; }
    public string? OgImage { get; set; }
    public bool SeoSettingsNoIndex { get; set; }
}

internal sealed class LegacyContactMessage : LegacyBaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public bool IsReplied { get; set; }
    public DateTime? RepliedAt { get; set; }
    public Guid? RepliedBy { get; set; }
    public string? ReplyContent { get; set; }
}

internal sealed class LegacyNewsletterSubscriber : LegacyBaseEntity
{
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? Source { get; set; }
}

internal sealed class LegacyCmsMediaAsset : LegacyBaseEntity
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string? AltText { get; set; }
}

internal sealed class LegacyCmsNavigationItem : LegacyBaseEntity
{
    public string Menu { get; set; } = "Main";
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Fragment { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; }
    public bool OpenInNewTab { get; set; }
}

internal sealed class LegacyCmsContentRevision : LegacyBaseEntity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public int Version { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
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
    public DateTime? PausedAt { get; set; }
    public int? TimeLimitSeconds { get; set; }
    public string ProcessedActionsJson { get; set; } = "{}";
}

internal sealed class LegacyStudentExerciseResult : LegacyBaseEntity
{
    public Guid StudentId { get; set; }
    public Guid ExerciseId { get; set; }
    public Guid? SessionId { get; set; }
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
    public string ResultDataJson { get; set; } = string.Empty;
    public int AttemptNumber { get; set; }
    public bool IsRetry { get; set; }
    public string DevicePlatform { get; set; } = string.Empty;
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public int TotalAttempts { get; set; }
    public decimal? AverageWPM { get; set; }
    public decimal? AverageComprehension { get; set; }
    public int DayOfWeek { get; set; }
    public TimeSpan TimeOfDay { get; set; }
    public decimal AverageResponseTimeMs { get; set; }
    public decimal MedianResponseTimeMs { get; set; }
    public decimal StdDevResponseTimeMs { get; set; }
    public int PauseCount { get; set; }
    public int TotalPausedSeconds { get; set; }
    public decimal PerformanceTrend { get; set; }
    public bool IsPersonalBest { get; set; }
    public decimal PreviousAverageScore { get; set; }
    public int CurrentStreak { get; set; }
    public decimal EngagementScore { get; set; }
    public decimal FrustrationScore { get; set; }
    public decimal LearningRate { get; set; }
    public decimal ConsistencyScore { get; set; }
}

internal sealed class LegacyLearningPathTemplate : LegacyBaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? TargetAgeGroupConfigurationId { get; set; }
    public string? Description { get; set; }
    public int TotalNodes { get; set; }
    public int EstimatedDays { get; set; }
    public bool IsActive { get; set; }
}

internal sealed class LegacyLearningPathNode : LegacyBaseEntity
{
    public Guid TemplateId { get; set; }
    public Guid? ParentNodeId { get; set; }
    public string NodeType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public Guid? ContentId { get; set; }
    public int Order { get; set; }
}

internal sealed class LegacyNodeContent : LegacyBaseEntity
{
    public Guid NodeId { get; set; }
    public Guid SourceContentId { get; set; }
    public string SourceContentType { get; set; } = string.Empty;
    public Guid? ExerciseId { get; set; }
    public Guid? ReadingTextId { get; set; }
    public string? Description { get; set; }
}

internal sealed class LegacyNodePrerequisite : LegacyBaseEntity
{
    public Guid NodeId { get; set; }
    public Guid PrerequisiteNodeId { get; set; }
}

internal sealed class LegacyStudentPathProgress : LegacyBaseEntity
{
    public Guid StudentId { get; set; }
    public Guid TemplateId { get; set; }
    public Guid? CurrentNodeId { get; set; }
    public decimal Progress { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public bool IsCompleted { get; set; }
}

internal sealed class LegacyStudentNodeProgress : LegacyBaseEntity
{
    public Guid StudentId { get; set; }
    public Guid NodeId { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal? Score { get; set; }
    public DateTime? CompletedAt { get; set; }
}

internal sealed class LegacyPersonalizedLearningPath : LegacyBaseEntity
{
    public Guid StudentId { get; set; }
    public Guid? TemplateId { get; set; }
    public int PathIndex { get; set; }
    public string ContentType { get; set; } = "Exercise";
    public Guid? ContentId { get; set; }
    public string ContentTitle { get; set; } = string.Empty;
    public int DifficultyLevel { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public decimal? AchievedScore { get; set; }
    public string? RecommendationReason { get; set; }
    public bool IsUnlocked { get; set; }
}

/// <summary>
/// Additive command ledger. It is intentionally separate from the legacy
/// content tables so enabling writes cannot alter existing business data.
/// </summary>
internal sealed class LegacyIdempotencyRecord
{
    public Guid Id { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public DateTime CreatedAt { get; set; }

    public bool Matches(string requestHash) =>
        string.Equals(RequestHash, requestHash, StringComparison.Ordinal);
}
