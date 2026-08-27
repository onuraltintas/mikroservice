using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Catalog;

public sealed class ReadingText : AggregateRoot
{
    private ReadingText()
    {
    }

    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public int WordCount { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public int DifficultyLevel { get; private set; }
    public Guid? TargetAgeGroupId { get; private set; }
    public string Language { get; private set; } = "tr";
    public bool IsActive { get; private set; } = true;
    public string Tags { get; private set; } = string.Empty;
    public int RecommendedMinLevel { get; private set; }
    public int RecommendedMaxLevel { get; private set; }
    public decimal AverageComprehensionScore { get; private set; }
    public int TimesRead { get; private set; }
    public int AverageReadingTimeSeconds { get; private set; }
    public Guid? ExerciseId { get; private set; }

    public static ReadingText Create(
        Guid id,
        string title,
        string content,
        string language = "tr",
        Guid? exerciseId = null,
        int? wordCount = null,
        int difficultyLevel = 0,
        string? category = null,
        Guid? targetAgeGroupId = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Reading text id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Reading text title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Reading text content is required.", nameof(content));
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Reading text language is required.", nameof(language));
        if (difficultyLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(difficultyLevel));

        return new ReadingText
        {
            Id = id,
            Title = title.Trim(),
            Content = content,
            WordCount = wordCount.GetValueOrDefault() > 0 ? wordCount!.Value : CountWords(content),
            Language = language.Trim(),
            ExerciseId = exerciseId,
            DifficultyLevel = difficultyLevel,
            Category = category?.Trim() ?? string.Empty,
            TargetAgeGroupId = targetAgeGroupId,
            IsActive = true
        };
    }

    public static ReadingText Import(
        Guid id,
        string title,
        string content,
        string language,
        Guid? exerciseId,
        int wordCount,
        string? category,
        int difficultyLevel,
        Guid? targetAgeGroupId,
        bool isActive,
        string? tags,
        int recommendedMinLevel,
        int recommendedMaxLevel,
        decimal averageComprehensionScore,
        int timesRead,
        int averageReadingTimeSeconds,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        var readingText = Create(
            id,
            title,
            content,
            language,
            exerciseId,
            wordCount,
            difficultyLevel,
            category,
            targetAgeGroupId);
        readingText.WordCount = wordCount > 0 ? wordCount : readingText.WordCount;
        readingText.IsActive = isActive;
        readingText.Tags = tags?.Trim() ?? string.Empty;
        readingText.RecommendedMinLevel = recommendedMinLevel;
        readingText.RecommendedMaxLevel = recommendedMaxLevel;
        readingText.AverageComprehensionScore = averageComprehensionScore;
        readingText.TimesRead = timesRead;
        readingText.AverageReadingTimeSeconds = averageReadingTimeSeconds;
        readingText.CreatedAt = createdAt;
        readingText.CreatedBy = createdBy;
        readingText.UpdatedAt = updatedAt;
        readingText.UpdatedBy = updatedBy;
        return readingText;
    }

    private static int CountWords(string content) =>
        content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
