namespace Identity.Application.Queries.GetRoles;

public record RoleDto(Guid Id, string Name, string Description, bool IsSystemRole, bool IsDeleted);
