using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.DeleteRole;

public record DeleteRoleCommand(Guid RoleId, bool Permanent) : IRequest<Result>;
