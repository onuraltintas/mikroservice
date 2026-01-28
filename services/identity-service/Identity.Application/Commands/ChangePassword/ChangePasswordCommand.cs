using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.ChangePassword;

public record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword
) : IRequest<Result>;
