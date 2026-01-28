using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.CreatePermission;

public record CreatePermissionCommand(string Key, string Description, string Group) : IRequest<Result<Guid>>;
