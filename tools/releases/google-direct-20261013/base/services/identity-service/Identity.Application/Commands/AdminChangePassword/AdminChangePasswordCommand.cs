using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.AdminChangePassword;

public record AdminChangePasswordCommand(Guid UserId, string NewPassword) : IRequest<Result>;
