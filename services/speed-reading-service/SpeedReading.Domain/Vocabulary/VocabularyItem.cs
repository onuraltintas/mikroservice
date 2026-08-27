using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Vocabulary;

public sealed class VocabularyItem : AggregateRoot
{
    private VocabularyItem()
    {
    }

    public string Word { get; private set; } = string.Empty;
    public string Definition { get; private set; } = string.Empty;
    public string? ExampleSentence { get; private set; }
    public string? Synonyms { get; private set; }
    public string? Antonyms { get; private set; }
    public Guid? TargetAgeGroupId { get; private set; }
    public int DifficultyLevel { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static VocabularyItem Create(Guid id, string word, string definition, string? exampleSentence,
        string? synonyms, string? antonyms, string category, int difficultyLevel, Guid? targetAgeGroupId,
        Guid actorId, DateTime createdAt)
    {
        Validate(id, word, definition, category, difficultyLevel);
        return new VocabularyItem
        {
            Id = id,
            Word = word.Trim(),
            Definition = definition.Trim(),
            ExampleSentence = Normalize(exampleSentence),
            Synonyms = Normalize(synonyms),
            Antonyms = Normalize(antonyms),
            Category = category.Trim(),
            DifficultyLevel = difficultyLevel,
            TargetAgeGroupId = targetAgeGroupId,
            CreatedAt = createdAt.Kind == DateTimeKind.Utc ? createdAt : createdAt.ToUniversalTime(),
            CreatedBy = actorId == Guid.Empty ? null : actorId.ToString()
        };
    }

    public static VocabularyItem Import(Guid id, string word, string definition, string? exampleSentence,
        string? synonyms, string? antonyms, Guid? targetAgeGroupId, int difficultyLevel, string category,
        Guid createdBy, DateTime createdAt, DateTime? updatedAt, Guid? updatedBy, bool isDeleted, DateTime? deletedAt, Guid? deletedBy)
    {
        var item = Create(id, word, definition, exampleSentence, synonyms, antonyms, category, difficultyLevel,
            targetAgeGroupId, createdBy, createdAt);
        item.UpdatedAt = updatedAt;
        item.UpdatedBy = updatedBy?.ToString();
        item.IsDeleted = isDeleted;
        item.DeletedAt = deletedAt;
        item.DeletedBy = deletedBy?.ToString();
        return item;
    }

    public void Update(string word, string definition, string? exampleSentence, string? synonyms, string? antonyms,
        string category, int difficultyLevel, Guid? targetAgeGroupId, Guid actorId, DateTime updatedAt)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Vocabulary actor is required.", nameof(actorId));
        Validate(Id, word, definition, category, difficultyLevel);
        Word = word.Trim();
        Definition = definition.Trim();
        ExampleSentence = Normalize(exampleSentence);
        Synonyms = Normalize(synonyms);
        Antonyms = Normalize(antonyms);
        Category = category.Trim();
        DifficultyLevel = difficultyLevel;
        TargetAgeGroupId = targetAgeGroupId;
        UpdatedAt = updatedAt.Kind == DateTimeKind.Utc ? updatedAt : updatedAt.ToUniversalTime();
        UpdatedBy = actorId.ToString();
    }

    public void Delete(Guid actorId, DateTime deletedAt)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Vocabulary actor is required.", nameof(actorId));
        IsDeleted = true;
        DeletedAt = deletedAt.Kind == DateTimeKind.Utc ? deletedAt : deletedAt.ToUniversalTime();
        DeletedBy = actorId.ToString();
        UpdatedAt = DeletedAt;
        UpdatedBy = DeletedBy;
    }

    private static void Validate(Guid id, string word, string definition, string category, int difficultyLevel)
    {
        if (id == Guid.Empty) throw new ArgumentException("Vocabulary id is required.");
        if (string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(definition) || string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Word, Definition and Category are required.");
        if (difficultyLevel is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(difficultyLevel));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
