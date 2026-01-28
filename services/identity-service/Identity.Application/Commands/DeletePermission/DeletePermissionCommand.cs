using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.DeletePermission;

public record DeletePermissionCommand(Guid Id, bool Permanent) : IRequest<Result>;
