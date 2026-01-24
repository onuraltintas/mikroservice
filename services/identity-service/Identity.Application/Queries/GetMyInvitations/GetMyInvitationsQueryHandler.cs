using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Queries.GetMyInvitations;

public class GetMyInvitationsQueryHandler : IRequestHandler<GetMyInvitationsQuery, Result<List<InvitationDto>>>
{
    private readonly IInvitationRepository _invitationRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetMyInvitationsQueryHandler(
        IInvitationRepository invitationRepository,
        ICurrentUserService currentUserService)
    {
        _invitationRepository = invitationRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<InvitationDto>>> Handle(GetMyInvitationsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Email == null)
        {
            return Result.Failure<List<InvitationDto>>(new Error("Auth.Unauthorized", "User is not authenticated"));
        }

        var invitations = await _invitationRepository.GetPendingByEmailAsync(_currentUserService.Email, cancellationToken);

        var dtos = invitations.Select(i => new InvitationDto
        {
            Id = i.Id,
            InviterEmail = "system@eduplatform.com", // TODO: Get inviter email
            Type = i.Type.ToString(),
            Status = i.Status.ToString(),
            Message = i.Message,
            CreatedAt = i.CreatedAt,
            ExpiresAt = i.ExpiresAt
        }).ToList();

        return Result.Success(dtos);
    }
}
