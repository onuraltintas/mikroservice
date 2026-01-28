using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Queries.GetRolePermissions;

public record GetRolePermissionsQuery(Guid RoleId) : IRequest<Result<RolePermissionsDto>>;

public record RolePermissionsDto(
    Guid RoleId,
    string RoleName,
    List<string> AssignedPermissions
);
