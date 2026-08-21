using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.GoogleLogin;

public sealed record LinkGoogleLoginCommand(string IdToken) : IRequest<Result>;
