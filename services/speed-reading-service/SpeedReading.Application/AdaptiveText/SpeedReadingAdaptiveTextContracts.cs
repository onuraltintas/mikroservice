namespace SpeedReading.Application.AdaptiveText;

public sealed record AdaptiveTextRecommendationSummary(
    Guid TextId,
    string Title,
    string Content,
    string Category,
    int DifficultyLevel,
    int WordCount,
    int EstimatedReadingTimeMinutes,
    IReadOnlyList<string> Tags,
    int RecommendedMinLevel,
    int RecommendedMaxLevel,
    decimal AverageComprehensionScore,
    int TimesRead,
    decimal TotalScore,
    decimal ConfidenceScore,
    IReadOnlyDictionary<string, decimal> ScoreBreakdown,
    string Reasoning);

public sealed record AdaptiveStudentReadingProfileSummary(
    Guid StudentId,
    int CurrentReadingLevel,
    decimal AverageComprehensionScore,
    decimal AverageReadingSpeed,
    int TotalTextsRead,
    int TotalReadingTimeSeconds,
    IReadOnlyList<string> PreferredCategories,
    IReadOnlyList<string> DifficultCategories,
    DateTime LastCalculatedAt);

public sealed record UpdateAdaptiveTextProfileRequest(
    Guid ReadingTextId,
    decimal ComprehensionScore,
    int ReadingTimeSeconds,
    int ReadingSpeedWpm);

public sealed record RecordAdaptiveTextRecommendationRequest(
    Guid ReadingTextId,
    decimal ConfidenceScore,
    string? ReasoningJson);

public interface ISpeedReadingAdaptiveText
{
    Task<IReadOnlyList<AdaptiveTextRecommendationSummary>> GetRecommendationsAsync(
        Guid studentId,
        int count,
        string? selectionCriteria,
        CancellationToken cancellationToken = default);

    Task<AdaptiveTextRecommendationSummary?> GetBestMatchAsync(
        Guid studentId,
        string? selectionCriteria,
        CancellationToken cancellationToken = default);

    Task<AdaptiveStudentReadingProfileSummary?> GetProfileAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<AdaptiveStudentReadingProfileSummary> UpdateProfileAsync(
        Guid studentId,
        UpdateAdaptiveTextProfileRequest request,
        CancellationToken cancellationToken = default);

    Task RecordRecommendationAsync(
        Guid studentId,
        RecordAdaptiveTextRecommendationRequest request,
        CancellationToken cancellationToken = default);
}
