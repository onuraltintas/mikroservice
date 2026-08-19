using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<RefreshTokenResponse>>;

public record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    string TokenType = "Bearer",
    int ExpiresInMinutes = 15);
