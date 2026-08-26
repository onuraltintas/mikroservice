namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacyStudentLearningProfile : LegacyBaseEntity
{
    public Guid StudentId { get; set; }
    public string ProficiencyLevel { get; set; } = "Beginner";
    public string? PreferredContentTypes { get; set; }
    public string LearningPace { get; set; } = "Normal";
    public string? WeakAreas { get; set; }
    public string? StrongAreas { get; set; }
}

internal sealed class LegacyContentRecommendation : LegacyBaseEntity
{
    public Guid StudentId { get; set; }
    public Guid ReadingTextId { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string? RecommendationReason { get; set; }
}

internal sealed class LegacyDailyGoal : LegacyBaseEntity
{
    public Guid StudentId { get; set; }
    public DateTime Date { get; set; }
    public int TargetMinutes { get; set; }
    public int ActualMinutes { get; set; }
    public bool IsCompleted { get; set; }
}
