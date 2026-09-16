using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;

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
    string ConfigurationJson,
    string EngineType = "");

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
    int OrderIndex,
    int QuestionType = 0);

internal static class AssessmentContentSnapshotRules
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AssessmentContentSnapshot? DeserializeOptional(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
            return null;

        AssessmentContentSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<AssessmentContentSnapshot>(json, JsonOptions);
        }
        catch (JsonException)
        {
            throw new BusinessRuleException(
                "SpeedReading.Assessment.ContentSnapshot.Invalid",
                "Assessment content snapshot is invalid; the assessment must be restarted.");
        }

        if (!IsValid(snapshot))
            throw new BusinessRuleException(
                "SpeedReading.Assessment.ContentSnapshot.Invalid",
                "Assessment content snapshot is incomplete or invalid; the assessment must be restarted.");

        return snapshot;
    }

    public static AssessmentContentSnapshot DeserializeRequired(string? json)
    {
        var snapshot = DeserializeOptional(json);
        return snapshot
            ?? throw new BusinessRuleException(
                "SpeedReading.Assessment.ContentSnapshot.Invalid",
                "Assessment content snapshot is required; the assessment must be restarted.");
    }

    private static bool IsValid(AssessmentContentSnapshot? snapshot)
    {
        if (snapshot is null
            || snapshot.Version <= 0
            || snapshot.Exercise is null
            || snapshot.Exercise.Id == Guid.Empty
            || string.IsNullOrWhiteSpace(snapshot.Exercise.Title)
            || string.IsNullOrWhiteSpace(snapshot.Exercise.TypeName)
            || snapshot.Questions is null)
            return false;

        if (snapshot.ReadingText is not null
            && (snapshot.ReadingText.Id == Guid.Empty
                || string.IsNullOrWhiteSpace(snapshot.ReadingText.Title)
                || string.IsNullOrWhiteSpace(snapshot.ReadingText.Content)))
            return false;

        if (snapshot.Questions.Count > 0 && snapshot.ReadingText is null)
            return false;

        var questionIds = snapshot.Questions
            .Where(question => question is not null)
            .Select(question => question.Id)
            .ToList();
        if (questionIds.Count != questionIds.Distinct().Count())
            return false;

        var readingTextId = snapshot.ReadingText?.Id;
        return snapshot.Questions.All(question =>
            question is not null
            && question.Id != Guid.Empty
            && readingTextId.HasValue
            && question.ReadingTextId == readingTextId.Value
            && !string.IsNullOrWhiteSpace(question.QuestionText)
            && question.CorrectAnswer?.Trim().ToUpperInvariant() is "A" or "B" or "C" or "D"
            && question.BloomLevel is >= 1 and <= 6
            && question.QuestionType is >= 1 and <= 3
            && question.OrderIndex >= 0);
    }
}
