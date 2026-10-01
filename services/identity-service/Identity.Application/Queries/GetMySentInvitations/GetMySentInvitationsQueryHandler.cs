using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Queries.GetMySentInvitations;

public sealed class GetMySentInvitationsQueryHandler(
    IInvitationRepository invitationRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetMySentInvitationsQuery, Result<List<SentInvitationDto>>>
{
    public async Task<Result<List<SentInvitationDto>>> Handle(
        GetMySentInvitationsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUserService.UserId.HasValue)
            return Result.Failure<List<SentInvitationDto>>(
                new Error("Auth.Unauthorized", "User is not authenticated"));

        var now = DateTime.UtcNow;
        var invitations = await invitationRepository.GetPendingByInviterIdAsync(
            currentUserService.UserId.Value,
            now,
            cancellationToken);

        return Result.Success(invitations
            .Where(invitation => invitation.InviterId == currentUserService.UserId.Value
                && invitation.Status == InvitationStatus.Pending
                && invitation.ExpiresAt > now)
            .Select(invitation => new SentInvitationDto(
                invitation.Id,
                invitation.InviteeEmail,
                invitation.Type.ToString(),
                invitation.CreatedAt,
                invitation.ExpiresAt))
            .ToList());
    }
}
