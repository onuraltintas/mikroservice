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
        // 16.11.2026: removing the Identity row alone left the user's records in every other service behind.
        // An account is erased through the erasure request flow (POST data-subject-requests/admin/erasure).
        if (request.Permanent)
        {
            return Result.Failure(new Error(
                "Identity.PermanentDeleteRetired",
                "Kalıcı silme bu yoldan yapılmıyor; hesabı ve verilerini silmek için silme talebi açın."));
        }

        return await _identityService.DeactivateUserAsync(request.UserId, cancellationToken);
    }
}
