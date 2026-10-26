using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.UpdatePermission;

public record UpdatePermissionCommand(Guid Id, string Description, string Group) : IRequest<Result>;
