using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.DeleteUser;

public record DeleteUserCommand(Guid UserId, bool HardDelete = false) : IRequest<Result>;
