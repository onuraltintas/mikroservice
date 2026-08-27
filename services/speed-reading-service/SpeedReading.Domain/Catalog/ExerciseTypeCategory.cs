using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Catalog;

public sealed class ExerciseTypeCategory : AggregateRoot
{
    private ExerciseTypeCategory()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static ExerciseTypeCategory Create(
        Guid id,
        string name,
        string displayName,
        string? description = null,
        int sortOrder = 0)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Category id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Category name and display name are required.");

        return new ExerciseTypeCategory
        {
            Id = id,
            Name = name.Trim(),
            DisplayName = displayName.Trim(),
            Description = description?.Trim() ?? string.Empty,
            SortOrder = sortOrder
        };
    }

    public static ExerciseTypeCategory Import(
        Guid id,
        string name,
        string displayName,
        string? description,
        int sortOrder,
        bool isActive,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        var category = Create(id, name, displayName, description, sortOrder);
        category.IsActive = isActive;
        category.CreatedAt = createdAt;
        category.CreatedBy = createdBy;
        category.UpdatedAt = updatedAt;
        category.UpdatedBy = updatedBy;
        return category;
    }
}
