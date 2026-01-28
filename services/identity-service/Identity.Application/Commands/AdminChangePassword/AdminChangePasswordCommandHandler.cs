using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.AdminChangePassword;

public class AdminChangePasswordCommandHandler : IRequestHandler<AdminChangePasswordCommand, Result>
{
    private readonly IIdentityService _identityService;

    public AdminChangePasswordCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Result> Handle(AdminChangePasswordCommand request, CancellationToken cancellationToken)
    {
        return await _identityService.ResetPasswordAsync(request.UserId, request.NewPassword, cancellationToken);
    }
}
