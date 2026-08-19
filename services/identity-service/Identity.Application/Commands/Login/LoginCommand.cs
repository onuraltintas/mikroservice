using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.Login;

public record LoginCommand(string Email, string Password) : IRequest<Result<LoginResponse>>;

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    string TokenType = "Bearer",
    int ExpiresInMinutes = 15);
