using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Exceptions;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.CancelSentInvitation;

public sealed class CancelSentInvitationCommandHandler(
    IInvitationRepository invitationRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService)
    : IRequestHandler<CancelSentInvitationCommand, Result>
{
    public async Task<Result> Handle(
        CancelSentInvitationCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated"));

        var invitation = await invitationRepository.GetByIdAsync(request.InvitationId, cancellationToken);
        if (invitation is null || invitation.InviterId != currentUserService.UserId.Value)
            return Result.Failure(new Error("Invitation.NotFound", "Invitation not found"));

        if (!invitation.IsPending())
        {
            if (invitation.Status == Identity.Domain.Enums.InvitationStatus.Pending && invitation.IsExpired())
            {
                invitation.MarkAsExpired();
                try
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (IdentityConcurrencyConflictException)
                {
                    // Accept/reject may have completed while this expired state was being saved.
                }
            }

            return Result.Failure(new Error("Invitation.NotPending", "Invitation is no longer pending"));
        }

        invitation.Cancel();
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (IdentityConcurrencyConflictException)
        {
            return Result.Failure(new Error("Invitation.NotPending", "Invitation is no longer pending"));
        }

        return Result.Success();
    }
}
