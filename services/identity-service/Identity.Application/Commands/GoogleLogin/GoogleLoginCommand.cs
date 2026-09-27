using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Commands.Login;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.GoogleLogin;

public record GoogleLoginCommand(
    string IdToken,
    string IpAddress,
    PlatformProduct? Product = null) : IRequest<Result<LoginResponse>>;
