namespace SpeedReading.Application.QuestionBank;

public interface ISpeedReadingQuestionBank
{
    Task<QuestionBankPage> GetQuestionsAsync(
        int pageNumber,
        int pageSize,
        int? examType,
        int? difficulty,
        int? category,
        string? searchTerm,
        Guid? ageGroupId,
        CancellationToken cancellationToken);

    Task<ExamQuestionSummary?> GetQuestionAsync(Guid id, CancellationToken cancellationToken);

    Task<Guid> CreateQuestionAsync(
        ExamQuestionRequest request,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<bool> UpdateQuestionAsync(
        Guid id,
        ExamQuestionRequest request,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<bool> DeleteQuestionAsync(Guid id, Guid actorId, CancellationToken cancellationToken);

    Task<bool> HardDeleteQuestionAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record ExamQuestionSummary(
    Guid Id,
    string Content,
    string Question,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD,
    string? OptionE,
    string CorrectOption,
    int ExamType,
    int Difficulty,
    int WordCount,
    string? Topic,
    int Category,
    Guid? TargetAgeGroupId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record QuestionBankPage(
    IReadOnlyList<ExamQuestionSummary> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);

public sealed record ExamQuestionRequest(
    string Content,
    string Question,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD,
    string? OptionE,
    string CorrectOption,
    int ExamType,
    int Difficulty,
    int WordCount,
    string? Topic,
    int Category,
    Guid? TargetAgeGroupId = null);
