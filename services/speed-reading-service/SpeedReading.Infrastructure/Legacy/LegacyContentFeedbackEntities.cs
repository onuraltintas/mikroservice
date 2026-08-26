namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacyUserContentFeedback : LegacyBaseEntity
{
    public Guid UserId { get; set; }
    public Guid ContentId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public bool IsLiked { get; set; }
    public bool IsBookmarked { get; set; }
    public string? SkipReason { get; set; }
    public decimal CompletionRate { get; set; }
    public int TimeSpentSeconds { get; set; }
    public int ExpectedTimeSeconds { get; set; }
    public decimal? ComprehensionScore { get; set; }
    public decimal? ExerciseScore { get; set; }
    public int RetryCount { get; set; }
    public int InteractionCount { get; set; }
    public int PauseCount { get; set; }
    public decimal? AbandonedAtPercentage { get; set; }
    public DateTime SessionDate { get; set; }
    public int TimeOfDay { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public string? ContentCategory { get; set; }
    public int? ContentDifficultyLevel { get; set; }
}
