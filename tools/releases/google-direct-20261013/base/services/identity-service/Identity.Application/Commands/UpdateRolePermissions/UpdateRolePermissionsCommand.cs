using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.UpdateRolePermissions;

public record UpdateRolePermissionsCommand(
    Guid RoleId,
    List<string> Permissions
) : IRequest<Result>;
