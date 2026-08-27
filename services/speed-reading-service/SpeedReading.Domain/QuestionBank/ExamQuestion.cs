using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.QuestionBank;

public sealed class ExamQuestion : AggregateRoot
{
    private ExamQuestion()
    {
    }

    public string Content { get; private set; } = string.Empty;
    public string Question { get; private set; } = string.Empty;
    public string OptionA { get; private set; } = string.Empty;
    public string OptionB { get; private set; } = string.Empty;
    public string OptionC { get; private set; } = string.Empty;
    public string OptionD { get; private set; } = string.Empty;
    public string? OptionE { get; private set; }
    public string CorrectOption { get; private set; } = string.Empty;
    public int ExamType { get; private set; }
    public int Difficulty { get; private set; }
    public int WordCount { get; private set; }
    public string? Topic { get; private set; }
    public int Category { get; private set; }
    public Guid? TargetAgeGroupId { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static ExamQuestion Create(
        Guid id,
        string content,
        string question,
        string optionA,
        string optionB,
        string optionC,
        string optionD,
        string? optionE,
        string correctOption,
        int examType,
        int difficulty,
        int wordCount,
        string? topic,
        int category,
        Guid? targetAgeGroupId,
        DateTime createdAt,
        Guid createdBy)
    {
        Validate(content, question, optionA, optionB, optionC, optionD, optionE, correctOption, examType, difficulty, category);
        if (id == Guid.Empty || createdBy == Guid.Empty)
            throw new ArgumentException("Question identifiers are required.");

        return new ExamQuestion
        {
            Id = id,
            Content = content.Trim(),
            Question = question.Trim(),
            OptionA = optionA.Trim(),
            OptionB = optionB.Trim(),
            OptionC = optionC.Trim(),
            OptionD = optionD.Trim(),
            OptionE = Normalize(optionE),
            CorrectOption = correctOption.Trim().ToUpperInvariant(),
            ExamType = examType,
            Difficulty = difficulty,
            WordCount = wordCount > 0 ? wordCount : CountWords(content),
            Topic = Normalize(topic),
            Category = category,
            TargetAgeGroupId = targetAgeGroupId,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy.ToString()
        };
    }

    public static ExamQuestion Import(
        Guid id,
        string content,
        string question,
        string optionA,
        string optionB,
        string optionC,
        string optionD,
        string? optionE,
        string correctOption,
        int examType,
        int difficulty,
        int wordCount,
        string? topic,
        int category,
        Guid? targetAgeGroupId,
        DateTime createdAt,
        Guid createdBy,
        DateTime? updatedAt,
        Guid? updatedBy,
        bool isDeleted,
        DateTime? deletedAt,
        Guid? deletedBy)
    {
        var item = Create(id, content, question, optionA, optionB, optionC, optionD, optionE,
            correctOption, examType, difficulty, wordCount, topic, category, targetAgeGroupId, createdAt, createdBy);
        item.UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null;
        item.UpdatedBy = updatedBy?.ToString();
        item.IsDeleted = isDeleted;
        item.DeletedAt = deletedAt.HasValue ? EnsureUtc(deletedAt.Value) : null;
        item.DeletedBy = deletedBy?.ToString();
        return item;
    }

    public void Update(
        string content,
        string question,
        string optionA,
        string optionB,
        string optionC,
        string optionD,
        string? optionE,
        string correctOption,
        int examType,
        int difficulty,
        int wordCount,
        string? topic,
        int category,
        Guid? targetAgeGroupId,
        Guid actorId,
        DateTime updatedAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Question actor is required.", nameof(actorId));
        Validate(content, question, optionA, optionB, optionC, optionD, optionE, correctOption, examType, difficulty, category);

        Content = content.Trim();
        Question = question.Trim();
        OptionA = optionA.Trim();
        OptionB = optionB.Trim();
        OptionC = optionC.Trim();
        OptionD = optionD.Trim();
        OptionE = Normalize(optionE);
        CorrectOption = correctOption.Trim().ToUpperInvariant();
        ExamType = examType;
        Difficulty = difficulty;
        WordCount = wordCount > 0 ? wordCount : CountWords(content);
        Topic = Normalize(topic);
        Category = category;
        TargetAgeGroupId = targetAgeGroupId;
        UpdatedAt = EnsureUtc(updatedAt);
        UpdatedBy = actorId.ToString();
    }

    public void Delete(Guid actorId, DateTime deletedAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Question actor is required.", nameof(actorId));
        IsDeleted = true;
        DeletedAt = EnsureUtc(deletedAt);
        DeletedBy = actorId.ToString();
        UpdatedAt = DeletedAt;
        UpdatedBy = DeletedBy;
    }

    private static void Validate(
        string content,
        string question,
        string optionA,
        string optionB,
        string optionC,
        string optionD,
        string? optionE,
        string correctOption,
        int examType,
        int difficulty,
        int category)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(question) ||
            string.IsNullOrWhiteSpace(optionA) || string.IsNullOrWhiteSpace(optionB) ||
            string.IsNullOrWhiteSpace(optionC) || string.IsNullOrWhiteSpace(optionD))
            throw new ArgumentException("Content, Question and options A-D are required.");
        if (examType is < 0 or > 6)
            throw new ArgumentOutOfRangeException(nameof(examType));
        if (difficulty is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficulty));
        if (category is < 0 or > 17)
            throw new ArgumentOutOfRangeException(nameof(category));

        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = optionA, ["B"] = optionB, ["C"] = optionC, ["D"] = optionD, ["E"] = optionE
        };
        if (!options.TryGetValue(correctOption.Trim(), out var answer) || string.IsNullOrWhiteSpace(answer))
            throw new ArgumentException("CorrectOption must reference a non-empty option.");
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static int CountWords(string value) => value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
