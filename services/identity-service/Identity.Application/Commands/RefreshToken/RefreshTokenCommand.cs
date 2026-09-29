using EduPlatform.Shared.Kernel.Results;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.RefreshToken;

public record RefreshTokenCommand(
    string RefreshToken,
    PlatformProduct? TargetProduct = null,
    Guid? ExpectedUserId = null) : IRequest<Result<RefreshTokenResponse>>;

public record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    bool IsPersistent,
    string TokenType = "Bearer",
    int ExpiresInMinutes = 15);
