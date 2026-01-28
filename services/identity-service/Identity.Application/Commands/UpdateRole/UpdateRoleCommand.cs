using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.UpdateRole;

public record UpdateRoleCommand(Guid RoleId, string Name, string Description) : IRequest<Result>;
