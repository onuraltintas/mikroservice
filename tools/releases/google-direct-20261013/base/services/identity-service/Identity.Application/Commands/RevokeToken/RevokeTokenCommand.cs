using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.RevokeToken;

public record RevokeTokenCommand(string Token, string IpAddress) : IRequest<Result>;
