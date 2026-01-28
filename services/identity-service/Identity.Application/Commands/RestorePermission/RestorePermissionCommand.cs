using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.RestorePermission;

public record RestorePermissionCommand(Guid Id) : IRequest<Result>;
