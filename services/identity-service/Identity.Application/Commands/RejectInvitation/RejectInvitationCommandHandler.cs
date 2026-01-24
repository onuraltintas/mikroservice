using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.RejectInvitation;

public class RejectInvitationCommandHandler : IRequestHandler<RejectInvitationCommand, Result>
{
    private readonly IInvitationRepository _invitationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public RejectInvitationCommandHandler(
        IInvitationRepository invitationRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _invitationRepository = invitationRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(RejectInvitationCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate current user is authenticated
        if (_currentUserService.Email == null)
        {
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated"));
        }

        var userEmail = _currentUserService.Email;

        // 2. Get invitation
        var invitation = await _invitationRepository.GetByIdAsync(request.InvitationId, cancellationToken);
        if (invitation == null)
        {
            return Result.Failure(new Error("Invitation.NotFound", "Invitation not found"));
        }

        // 3. Verify invitation is for current user
        if (!invitation.InviteeEmail.Equals(userEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(new Error("Invitation.Forbidden", "This invitation is not for you"));
        }

        // 4. Verify invitation is still pending
        if (!invitation.IsPending())
        {
            return Result.Failure(new Error("Invitation.NotPending", "Invitation is no longer pending"));
        }

        // 5. Reject invitation
        invitation.Reject();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
