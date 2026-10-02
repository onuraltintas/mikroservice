namespace Coaching.Domain.Entities;

public sealed class StudyCatalogLesson
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Source { get; private set; } = string.Empty;
    public string SourceId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int? GradeNumber { get; private set; }
    public string? ExamCode { get; private set; }
    public bool IsActive { get; private set; }
    private StudyCatalogLesson() { }

    public void SetActive(bool active) => IsActive = active;

    public void Edit(string name, int? gradeNumber, string? examCode)
    {
        var validated = Create(Source, SourceId, name, gradeNumber, examCode);
        Name = validated.Name;
        GradeNumber = validated.GradeNumber;
        ExamCode = validated.ExamCode;
    }

    public static StudyCatalogLesson Create(string source, string sourceId, string name, int? gradeNumber, string? examCode)
    {
        if (gradeNumber is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(gradeNumber));
        var code = string.IsNullOrWhiteSpace(examCode) ? null : examCode.Trim().ToUpperInvariant();
        if (code is not (null or "LGS" or "TYT" or "AYT" or "YDT" or "TDP")) throw new ArgumentException("Unknown exam code.", nameof(examCode));
        return new StudyCatalogLesson { Source = StudyCatalogText.Require(source, 100),
            SourceId = StudyCatalogText.Require(sourceId, 100), Name = StudyCatalogText.Require(name, 300),
            GradeNumber = gradeNumber, ExamCode = code };
    }
}

public sealed class StudyCatalogUnit
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Source { get; private set; } = string.Empty;
    public string SourceId { get; private set; } = string.Empty;
    public Guid LessonId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int? DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }
    private StudyCatalogUnit() { }

    public void SetActive(bool active) => IsActive = active;

    public void Edit(string name, int? displayOrder)
    {
        var validated = Create(Source, SourceId, LessonId, name, displayOrder);
        Name = validated.Name;
        DisplayOrder = validated.DisplayOrder;
    }

    public static StudyCatalogUnit Create(string source, string sourceId, Guid lessonId, string name, int? displayOrder)
    {
        if (lessonId == Guid.Empty) throw new ArgumentException("Lesson is required.", nameof(lessonId));
        if (displayOrder < 0) throw new ArgumentOutOfRangeException(nameof(displayOrder));
        return new StudyCatalogUnit { Source = StudyCatalogText.Require(source, 100),
            SourceId = StudyCatalogText.Require(sourceId, 100), LessonId = lessonId,
            Name = StudyCatalogText.Require(name, 300), DisplayOrder = displayOrder };
    }
}

// Parentless entries represent upper topics; children represent individual study topics.
public sealed class StudyCatalogTopic
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Source { get; private set; } = string.Empty;
    public string SourceId { get; private set; } = string.Empty;
    public Guid LessonId { get; private set; }
    public Guid UnitId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int? DisplayOrder { get; private set; }
    public int? EstimatedMinutes { get; private set; }
    public bool IsActive { get; private set; }
    private StudyCatalogTopic() { }

    public void SetActive(bool active) => IsActive = active;

    public void Edit(string name, int? displayOrder, int? estimatedMinutes)
    {
        var validated = Create(Source, SourceId, LessonId, UnitId, name, ParentId, displayOrder, estimatedMinutes);
        Name = validated.Name;
        DisplayOrder = validated.DisplayOrder;
        EstimatedMinutes = validated.EstimatedMinutes;
    }

    public static StudyCatalogTopic Create(string source, string sourceId, Guid lessonId, Guid unitId,
        string name, Guid? parentId, int? displayOrder, int? estimatedMinutes = null)
    {
        if (lessonId == Guid.Empty || unitId == Guid.Empty || parentId == Guid.Empty)
            throw new ArgumentException("Valid catalog ownership is required.");
        if (displayOrder < 0) throw new ArgumentOutOfRangeException(nameof(displayOrder));
        if (estimatedMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(estimatedMinutes));
        return new StudyCatalogTopic { Source = StudyCatalogText.Require(source, 100),
            SourceId = StudyCatalogText.Require(sourceId, 100), LessonId = lessonId, UnitId = unitId,
            Name = StudyCatalogText.Require(name, 300), ParentId = parentId,
            DisplayOrder = displayOrder, EstimatedMinutes = estimatedMinutes };
    }
}

internal static class StudyCatalogText
{
    internal static string Require(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength)
            throw new ArgumentException($"Catalog text must contain 1 to {maxLength} characters.");
        return value.Trim();
    }
}
