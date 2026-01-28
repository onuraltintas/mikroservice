using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Domain.Entities;

public class Permission : Entity
{
    public string Key { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Group { get; private set; } = string.Empty;
    public bool IsSystem { get; private set; }
    public bool IsDeleted { get; private set; }

    private Permission() { }

    public static Permission Create(string key, string description, string group, bool isSystem = false)
    {
        return new Permission
        {
            Id = Guid.NewGuid(),
            Key = key,
            Description = description,
            Group = group,
            IsSystem = isSystem,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string description, string group)
    {
        Description = description;
        Group = group;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsDeleted()
    {
        if (IsSystem)
        {
            throw new InvalidOperationException("Cannot delete a system permission.");
        }
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        IsDeleted = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
