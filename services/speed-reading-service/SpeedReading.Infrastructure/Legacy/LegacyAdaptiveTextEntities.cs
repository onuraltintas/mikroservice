namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacyStudentReadingProfile : LegacyBaseEntity
{
    public Guid StudentId { get; set; }
    public int CurrentReadingLevel { get; set; }
    public decimal AverageComprehensionScore { get; set; }
    public decimal AverageReadingSpeed { get; set; }
    public int TotalTextsRead { get; set; }
    public int TotalReadingTimeSeconds { get; set; }
    public List<string> PreferredCategories { get; set; } = [];
    public List<string> DifficultCategories { get; set; } = [];
    public string? PreferredCategoriesSource { get; set; }
    public string? DifficultCategoriesSource { get; set; }
    public DateTime LastCalculatedAt { get; set; }
}

internal sealed class LegacyTextRecommendationHistory : LegacyBaseEntity
{
    public Guid StudentId { get; set; }
    public Guid ReadingTextId { get; set; }
    public DateTime RecommendedAt { get; set; }
    public bool WasAccepted { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string ReasoningJson { get; set; } = "{}";
    public int StudentLevelAtTime { get; set; }
    public decimal? ResultScore { get; set; }
    public DateTime? CompletedAt { get; set; }
}
