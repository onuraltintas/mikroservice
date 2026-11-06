using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Exceptions;
using Identity.Application.Interfaces;
using Identity.Application.Services;
using MediatR;

namespace Identity.Application.Commands.RejectInvitation;

public class RejectInvitationCommandHandler : IRequestHandler<RejectInvitationCommand, Result>
{
    private readonly IInvitationRepository _invitationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;

    public RejectInvitationCommandHandler(
        IInvitationRepository invitationRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IUserRepository userRepository)
    {
        _invitationRepository = invitationRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _userRepository = userRepository;
    }

    public async Task<Result> Handle(RejectInvitationCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate current user is authenticated. Paket F L5: the address is read from the account (by the token's
        // subject), not from the token's e-mail claim, which stays stale after a confirmed e-mail change until refresh.
        var userEmail = await CurrentUserEmail.ReadAsync(_currentUserService, _userRepository, cancellationToken);
        if (userEmail == null)
        {
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated"));
        }

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
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (IdentityConcurrencyConflictException)
        {
            return Result.Failure(new Error("Invitation.NotPending", "Invitation is no longer pending"));
        }

        return Result.Success();
    }
}
