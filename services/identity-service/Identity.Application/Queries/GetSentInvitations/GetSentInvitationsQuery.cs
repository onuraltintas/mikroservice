using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Queries.GetSentInvitations;

public sealed record SentInvitationDto(
    Guid Id,
    string InviteeEmail,
    string Type,
    string Status,
    DateTime CreatedAt,
    DateTime ExpiresAt);

public sealed record GetSentInvitationsQuery : IRequest<Result<List<SentInvitationDto>>>;

public sealed class GetSentInvitationsQueryHandler(
    IInvitationRepository invitations,
    ICurrentUserService currentUser) : IRequestHandler<GetSentInvitationsQuery, Result<List<SentInvitationDto>>>
{
    public async Task<Result<List<SentInvitationDto>>> Handle(
        GetSentInvitationsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<List<SentInvitationDto>>(
                new Error("Auth.Unauthorized", "User is not authenticated"));
        }

        var sent = await invitations.GetByInviterIdAsync(userId, cancellationToken);
        return Result.Success(sent.Select(invitation => new SentInvitationDto(
            invitation.Id,
            invitation.InviteeEmail,
            invitation.Type.ToString(),
            invitation.IsExpired() && invitation.Status == Identity.Domain.Enums.InvitationStatus.Pending
                ? Identity.Domain.Enums.InvitationStatus.Expired.ToString()
                : invitation.Status.ToString(),
            invitation.CreatedAt,
            invitation.ExpiresAt)).ToList());
    }
}
