namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacyVocabularyItem : LegacyBaseEntity
{
    public string Word { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string? ExampleSentence { get; set; }
    public string? Synonyms { get; set; }
    public string? Antonyms { get; set; }
    public Guid? TargetAgeGroupConfigurationId { get; set; }
    public int DifficultyLevel { get; set; }
    public string Category { get; set; } = string.Empty;
}

internal sealed class LegacyUserVocabularyProgress : LegacyBaseEntity
{
    public Guid UserId { get; set; }
    public Guid VocabularyItemId { get; set; }
    public int Box { get; set; }
    public int ConsecutiveCorrectCount { get; set; }
    public DateTime NextReviewDate { get; set; }
    public DateTime LastReviewedAt { get; set; }
}
