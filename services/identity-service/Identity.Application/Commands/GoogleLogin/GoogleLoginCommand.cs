using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Commands.Login;
using MediatR;

namespace Identity.Application.Commands.GoogleLogin;

public record GoogleLoginCommand(string IdToken, string IpAddress) : IRequest<Result<LoginResponse>>;
