using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.DeleteUser;

public record DeleteUserCommand(Guid UserId, bool Permanent = false) : IRequest<Result>;
