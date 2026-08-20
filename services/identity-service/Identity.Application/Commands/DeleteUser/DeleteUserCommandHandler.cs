using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using MediatR;
using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Application.Commands.DeleteUser;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Result>
{
    private readonly IIdentityService _identityService;

    public DeleteUserCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Result> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        return request.Permanent
            ? await _identityService.DeleteUserAsync(request.UserId, cancellationToken)
            : await _identityService.DeactivateUserAsync(request.UserId, cancellationToken);
    }
}
