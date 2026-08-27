using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Catalog;

public sealed class ReadingQuestion : Entity
{
    private ReadingQuestion()
    {
    }

    public Guid ReadingTextId { get; private set; }
    public string QuestionText { get; private set; } = string.Empty;
    public int Type { get; private set; }
    public int BloomLevel { get; private set; }
    public int DifficultyLevel { get; private set; }
    public string? Explanation { get; private set; }
    public string OptionA { get; private set; } = string.Empty;
    public string OptionB { get; private set; } = string.Empty;
    public string OptionC { get; private set; } = string.Empty;
    public string OptionD { get; private set; } = string.Empty;
    public string CorrectAnswer { get; private set; } = string.Empty;
    public int OrderIndex { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static ReadingQuestion Create(
        Guid id,
        Guid readingTextId,
        string questionText,
        string correctAnswer,
        int orderIndex,
        int type = 0,
        int bloomLevel = 0,
        int difficultyLevel = 0,
        string? explanation = null,
        string? optionA = null,
        string? optionB = null,
        string? optionC = null,
        string? optionD = null,
        bool allowMissingCorrectAnswer = false)
    {
        if (id == Guid.Empty || readingTextId == Guid.Empty)
            throw new ArgumentException("Question and reading text ids are required.");
        if (string.IsNullOrWhiteSpace(questionText))
            throw new ArgumentException("Question text is required.", nameof(questionText));
        if (string.IsNullOrWhiteSpace(correctAnswer) && !allowMissingCorrectAnswer)
            throw new ArgumentException("Correct answer is required.", nameof(correctAnswer));
        if (orderIndex < 0 || bloomLevel < 0 || difficultyLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(orderIndex));

        return new ReadingQuestion
        {
            Id = id,
            ReadingTextId = readingTextId,
            QuestionText = questionText.Trim(),
            CorrectAnswer = correctAnswer.Trim(),
            OrderIndex = orderIndex,
            Type = type,
            BloomLevel = bloomLevel,
            DifficultyLevel = difficultyLevel,
            Explanation = explanation?.Trim(),
            OptionA = optionA?.Trim() ?? string.Empty,
            OptionB = optionB?.Trim() ?? string.Empty,
            OptionC = optionC?.Trim() ?? string.Empty,
            OptionD = optionD?.Trim() ?? string.Empty
        };
    }

    public static ReadingQuestion Import(
        Guid id,
        Guid readingTextId,
        string questionText,
        string correctAnswer,
        int orderIndex,
        int type,
        int bloomLevel,
        int difficultyLevel,
        string? explanation,
        string? optionA,
        string? optionB,
        string? optionC,
        string? optionD,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        var question = Create(
            id,
            readingTextId,
            questionText,
            correctAnswer,
            orderIndex,
            type,
            bloomLevel,
            difficultyLevel,
            explanation,
            optionA,
            optionB,
            optionC,
            optionD,
            allowMissingCorrectAnswer: true);
        question.CreatedAt = createdAt;
        question.CreatedBy = createdBy;
        question.UpdatedAt = updatedAt;
        question.UpdatedBy = updatedBy;
        return question;
    }

    public void Update(
        string questionText,
        int type,
        int bloomLevel,
        int difficultyLevel,
        string? explanation,
        string optionA,
        string optionB,
        string optionC,
        string optionD,
        string correctAnswer,
        int orderIndex,
        Guid actorId,
        DateTime updatedAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Reading question actor is required.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(questionText) || string.IsNullOrWhiteSpace(correctAnswer)
            || orderIndex < 0 || bloomLevel < 0 || difficultyLevel < 0)
            throw new ArgumentException("Reading question fields are invalid.");

        QuestionText = questionText.Trim();
        Type = type;
        BloomLevel = bloomLevel;
        DifficultyLevel = difficultyLevel;
        Explanation = explanation?.Trim();
        OptionA = optionA?.Trim() ?? string.Empty;
        OptionB = optionB?.Trim() ?? string.Empty;
        OptionC = optionC?.Trim() ?? string.Empty;
        OptionD = optionD?.Trim() ?? string.Empty;
        CorrectAnswer = correctAnswer.Trim();
        OrderIndex = orderIndex;
        UpdatedAt = updatedAt.Kind == DateTimeKind.Utc ? updatedAt : updatedAt.ToUniversalTime();
        UpdatedBy = actorId.ToString();
    }

    public void Delete(Guid actorId, DateTime deletedAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Reading question actor is required.", nameof(actorId));
        IsDeleted = true;
        DeletedAt = deletedAt.Kind == DateTimeKind.Utc ? deletedAt : deletedAt.ToUniversalTime();
        DeletedBy = actorId.ToString();
        UpdatedAt = DeletedAt;
        UpdatedBy = actorId.ToString();
    }
}
