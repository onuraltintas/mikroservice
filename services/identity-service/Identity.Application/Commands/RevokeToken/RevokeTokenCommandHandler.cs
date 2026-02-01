using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using MediatR;
using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Application.Commands.RevokeToken;

public class RevokeTokenCommandHandler : IRequestHandler<RevokeTokenCommand, Result>
{
    private readonly IIdentityService _identityService;

    public RevokeTokenCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Result> Handle(RevokeTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return Result.Failure(new Error("RevokeToken.Empty", "Token is required."));

        // "Logged Out" is the standard reason for manual revocation
        return await _identityService.RevokeRefreshTokenAsync(request.Token, request.IpAddress, "Logged Out", cancellationToken);
    }
}
