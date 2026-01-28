using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.RefreshToken;

public record RefreshTokenCommand(string Token, string RefreshToken) : IRequest<Result<RefreshTokenResponse>>;

public record RefreshTokenResponse(string AccessToken, string RefreshToken);
