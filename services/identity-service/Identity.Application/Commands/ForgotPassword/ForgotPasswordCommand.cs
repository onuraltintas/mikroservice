using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.ForgotPassword;

/// <param name="Product">PR #111 final review: the site asking ("coaching" / "speed-reading"), so the reset
/// e-mail and its link are that product's; see <see cref="Identity.Application.Registration.EmailProduct"/>.</param>
public record ForgotPasswordCommand(string Email, string? Product = null) : IRequest<Result>;
