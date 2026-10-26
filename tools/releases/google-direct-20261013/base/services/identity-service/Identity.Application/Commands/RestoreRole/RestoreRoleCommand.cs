using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.RestoreRole;

public record RestoreRoleCommand(Guid RoleId) : IRequest<Result>;
