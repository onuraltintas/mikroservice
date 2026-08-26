namespace SpeedReading.Application.StudentReading;

public sealed record StudentReadingTextSummary(
    Guid Id,
    string Title,
    string Category,
    int DifficultyLevel,
    int WordCount,
    string Language);

public sealed record StudentReadingQuestion(
    Guid Id,
    Guid ReadingTextId,
    string QuestionText,
    int Type,
    int BloomLevel,
    int DifficultyLevel,
    string? Explanation,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD,
    string CorrectAnswer,
    int OrderIndex);

public sealed record StudentReadingStart(
    Guid Id,
    string Title,
    string Content,
    string Category,
    int DifficultyLevel,
    int WordCount,
    IReadOnlyList<StudentReadingQuestion> Questions);

public sealed record CompleteStudentReadingRequest(
    int TimeSpentSeconds,
    decimal ComprehensionScore,
    IReadOnlyList<StudentReadingAnswer>? Answers);

public sealed record StudentReadingAnswer(Guid QuestionId, string SelectedAnswer);

public sealed record StudentReadingCompletion(
    Guid SessionId,
    int ReadingTimeSeconds,
    int CalculatedWPM,
    int CorrectAnswers,
    int TotalQuestions,
    decimal ComprehensionRate,
    decimal EfficiencyScore,
    string PerformanceLevel);

public sealed record StudentReadingHistoryItem(
    Guid Id,
    Guid ReadingTextId,
    string ReadingTextTitle,
    string Category,
    int ReadingTimeSeconds,
    int CalculatedWPM,
    int CorrectAnswers,
    int TotalQuestions,
    decimal ComprehensionRate,
    decimal EfficiencyScore,
    DateTime CompletedAt,
    string PerformanceLevel);

public sealed record StudentReadingSessionDetails(
    Guid Id,
    Guid ReadingTextId,
    int CalculatedWPM,
    decimal ComprehensionRate,
    int ReadingTimeSeconds,
    DateTime CompletedAt);

public sealed record StudentReadingStatistics(
    int TotalSessions,
    decimal AverageWPM,
    decimal AverageComprehension,
    decimal AverageEfficiency,
    int TextsCompleted,
    int TotalTimeMinutes,
    IReadOnlyList<string> CategoriesRead,
    IReadOnlyList<StudentReadingHistoryItem> RecentSessions);

public sealed record StudentReadingWpmPoint(DateTime Date, int Wpm);

public sealed record StudentReadingComprehensionPoint(DateTime Date, decimal Rate);

public interface ISpeedReadingStudentReading
{
    Task<IReadOnlyList<string>> GetCategoriesAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentReadingTextSummary>> GetAvailableTextsAsync(Guid userId, string? category, int? minLevel, int? maxLevel, int? specificLevel, CancellationToken cancellationToken);
    Task<StudentReadingStart?> StartAsync(Guid textId, CancellationToken cancellationToken);
    Task<StudentReadingCompletion?> CompleteAsync(Guid userId, Guid textId, CompleteStudentReadingRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentReadingHistoryItem>> GetHistoryAsync(Guid userId, Guid? readingTextId, DateTime? dateFrom, DateTime? dateTo, string? category, CancellationToken cancellationToken);
    Task<StudentReadingSessionDetails?> GetSessionDetailsAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
    Task<StudentReadingStatistics> GetStatisticsAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentReadingWpmPoint>> GetWpmProgressionAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentReadingComprehensionPoint>> GetComprehensionProgressionAsync(Guid userId, CancellationToken cancellationToken);
}
