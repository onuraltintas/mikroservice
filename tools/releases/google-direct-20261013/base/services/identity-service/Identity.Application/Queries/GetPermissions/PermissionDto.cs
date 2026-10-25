namespace Identity.Application.Queries.GetPermissions;

public record PermissionDto(Guid Id, string Key, string Description, string Group, bool IsSystem, bool IsDeleted);
