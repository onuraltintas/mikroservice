using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Visualization;

public sealed class VisualizationQuestion : AggregateRoot
{
    private VisualizationQuestion()
    {
    }

    public Guid SceneId { get; private set; }
    public string QuestionText { get; private set; } = string.Empty;
    public string OptionsJson { get; private set; } = "[]";
    public string CorrectAnswer { get; private set; } = string.Empty;
    public string QuestionType { get; private set; } = "detail";
    public int DisplayOrder { get; private set; }
    public string? HintText { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static VisualizationQuestion Create(Guid id, Guid sceneId, string questionText, string optionsJson,
        string correctAnswer, string questionType, int displayOrder, string? hintText, Guid actorId, DateTime createdAt)
    {
        Validate(id, sceneId, questionText, optionsJson, correctAnswer, questionType, displayOrder);
        return new VisualizationQuestion
        {
            Id = id,
            SceneId = sceneId,
            QuestionText = questionText.Trim(),
            OptionsJson = optionsJson,
            CorrectAnswer = correctAnswer.Trim(),
            QuestionType = questionType.Trim().ToLowerInvariant(),
            DisplayOrder = displayOrder,
            HintText = string.IsNullOrWhiteSpace(hintText) ? null : hintText.Trim(),
            CreatedAt = createdAt.Kind == DateTimeKind.Utc ? createdAt : createdAt.ToUniversalTime(),
            CreatedBy = actorId == Guid.Empty ? null : actorId.ToString()
        };
    }

    public static VisualizationQuestion Import(Guid id, Guid sceneId, string questionText, string optionsJson,
        string correctAnswer, string questionType, int displayOrder, string? hintText, Guid createdBy, DateTime createdAt,
        DateTime? updatedAt, Guid? updatedBy, bool isDeleted, DateTime? deletedAt, Guid? deletedBy)
    {
        var item = Create(id, sceneId, questionText, optionsJson, correctAnswer, questionType, displayOrder, hintText, createdBy, createdAt);
        item.UpdatedAt = updatedAt;
        item.UpdatedBy = updatedBy?.ToString();
        item.IsDeleted = isDeleted;
        item.DeletedAt = deletedAt;
        item.DeletedBy = deletedBy?.ToString();
        return item;
    }

    public void Delete(Guid actorId, DateTime deletedAt)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Question actor is required.", nameof(actorId));
        IsDeleted = true;
        DeletedAt = deletedAt.Kind == DateTimeKind.Utc ? deletedAt : deletedAt.ToUniversalTime();
        DeletedBy = actorId.ToString();
        UpdatedAt = DeletedAt;
        UpdatedBy = DeletedBy;
    }

    private static void Validate(Guid id, Guid sceneId, string questionText, string optionsJson, string correctAnswer,
        string questionType, int displayOrder)
    {
        if (id == Guid.Empty || sceneId == Guid.Empty) throw new ArgumentException("Question identifiers are required.");
        if (string.IsNullOrWhiteSpace(questionText) || string.IsNullOrWhiteSpace(correctAnswer))
            throw new ArgumentException("Question text and correct answer are required.");
        if (string.IsNullOrWhiteSpace(optionsJson)) throw new ArgumentException("Question options are required.");
        if (string.IsNullOrWhiteSpace(questionType)) throw new ArgumentException("Question type is required.");
        if (displayOrder < 0) throw new ArgumentOutOfRangeException(nameof(displayOrder));
    }
}
