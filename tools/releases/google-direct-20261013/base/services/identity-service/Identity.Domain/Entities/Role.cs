using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Domain.Entities;

public class Role : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsSystemRole { get; private set; }
    public bool IsDeleted { get; private set; }
    
    // Permissions
    public virtual ICollection<RolePermission> Permissions { get; private set; } = new List<RolePermission>();

    private Role() { }

    public static Role Create(string name, string description, bool isSystemRole = false)
    {
        return new Role
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            IsSystemRole = isSystemRole,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string description)
    {
        if (IsSystemRole && Name != name)
        {
            throw new InvalidOperationException("Cannot change the name of a system role.");
        }
        Name = name;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsDeleted()
    {
        if (IsSystemRole)
        {
            throw new InvalidOperationException("Cannot delete a system role.");
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
