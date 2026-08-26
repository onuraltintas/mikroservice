namespace SpeedReading.Application.Vocabulary;

public interface ISpeedReadingVocabulary
{
    Task<VocabularyPage> GetItemsAsync(
        string? search,
        string? category,
        int? difficultyLevel,
        Guid? ageGroupId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<VocabularyItemSummary?> GetItemAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken);

    Task<VocabularyItemSummary> CreateItemAsync(
        VocabularyItemRequest request,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<VocabularyItemSummary?> UpdateItemAsync(
        Guid id,
        VocabularyItemRequest request,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<bool> DeleteItemAsync(Guid id, Guid actorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserVocabularySummary>> GetUserVocabularyAsync(
        Guid userId,
        int? status,
        CancellationToken cancellationToken);

    Task<UserVocabularySummary?> AddToUserVocabularyAsync(
        Guid userId,
        Guid vocabularyItemId,
        CancellationToken cancellationToken);

    Task<bool> UpdateUserVocabularyAsync(
        Guid userId,
        Guid progressId,
        bool isCorrect,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UserVocabularySummary>> GetDueForReviewAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<VocabularyImportResult> ImportCsvAsync(
        Stream csv,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<byte[]> ExportCsvAsync(
        string? category,
        int? difficultyLevel,
        Guid? ageGroupId,
        CancellationToken cancellationToken);
}

public sealed record VocabularyItemSummary(
    Guid Id,
    string Word,
    string Definition,
    string? ExampleSentence,
    string? Synonyms,
    string? Antonyms,
    Guid? TargetAgeGroupId,
    string? TargetAgeGroup,
    int DifficultyLevel,
    string Category,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record VocabularyPage(
    IReadOnlyList<VocabularyItemSummary> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);

public sealed record VocabularyItemRequest(
    string Word,
    string Definition,
    string? ExampleSentence,
    string? Synonyms,
    string? Antonyms,
    string Category,
    int DifficultyLevel,
    Guid? TargetAgeGroupId);

public sealed record UserVocabularySummary(
    Guid Id,
    Guid UserId,
    Guid VocabularyItemId,
    int Status,
    int CorrectAttempts,
    int IncorrectAttempts,
    DateTime LastReviewedAt,
    DateTime NextReviewAt,
    DateTime CreatedAt,
    VocabularyItemSummary? VocabularyItem);

public sealed record VocabularyImportResult(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<string> Errors);
