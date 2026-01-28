using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Domain.Entities;

public class RolePermission : Entity
{
    public Guid RoleId { get; private set; }
    public string Permission { get; private set; } = string.Empty;

    // Navigation Property
    public virtual Role Role { get; private set; } = null!;

    private RolePermission() { } // For EF Core

    public RolePermission(Guid roleId, string permission)
    {
        Id = Guid.NewGuid();
        RoleId = roleId;
        Permission = permission;
        CreatedAt = DateTime.UtcNow;
    }
}
