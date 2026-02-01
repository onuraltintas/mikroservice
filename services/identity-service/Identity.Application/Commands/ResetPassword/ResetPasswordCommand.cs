using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.ResetPassword;

public record ResetPasswordCommand(
    string Email,
    string Token,
    string NewPassword) : IRequest<Result>;
