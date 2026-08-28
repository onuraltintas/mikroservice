namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// Server-side immutable assessment content. It is stored with the form item
/// so an assessment remains reproducible even when catalog content changes.
/// </summary>
internal sealed record AssessmentContentSnapshot(
    int Version,
    AssessmentExerciseSnapshot Exercise,
    AssessmentReadingTextSnapshot? ReadingText,
    IReadOnlyList<AssessmentQuestionSnapshot> Questions);

internal sealed record AssessmentExerciseSnapshot(
    Guid Id,
    string Title,
    string Description,
    string TypeName,
    int DifficultyLevel,
    string ConfigurationJson);

internal sealed record AssessmentReadingTextSnapshot(
    Guid Id,
    string Title,
    string Content,
    int WordCount);

internal sealed record AssessmentQuestionSnapshot(
    Guid ReadingTextId,
    Guid Id,
    string QuestionText,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD,
    string CorrectAnswer,
    string? Explanation,
    int BloomLevel,
    int DifficultyLevel,
    int OrderIndex);
