using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Application.Services;
using MediatR;

namespace Identity.Application.Queries.GetMyInvitations;

public class GetMyInvitationsQueryHandler : IRequestHandler<GetMyInvitationsQuery, Result<List<InvitationDto>>>
{
    private readonly IInvitationRepository _invitationRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;

    public GetMyInvitationsQueryHandler(
        IInvitationRepository invitationRepository,
        ICurrentUserService currentUserService,
        IUserRepository userRepository)
    {
        _invitationRepository = invitationRepository;
        _currentUserService = currentUserService;
        _userRepository = userRepository;
    }

    public async Task<Result<List<InvitationDto>>> Handle(GetMyInvitationsQuery request, CancellationToken cancellationToken)
    {
        // Paket F L5: the account's current address (by the token's subject), not the possibly stale e-mail claim.
        var email = await CurrentUserEmail.ReadAsync(_currentUserService, _userRepository, cancellationToken);
        if (email == null)
        {
            return Result.Failure<List<InvitationDto>>(new Error("Auth.Unauthorized", "User is not authenticated"));
        }

        var invitations = await _invitationRepository.GetPendingWithInviterEmailAsync(
            email,
            cancellationToken);

        var dtos = invitations.Select(i => new InvitationDto
        {
            Id = i.Id,
            InviterEmail = i.InviterEmail,
            Type = i.Type.ToString(),
            Status = i.Status.ToString(),
            Message = i.Message,
            CreatedAt = i.CreatedAt,
            ExpiresAt = i.ExpiresAt
        }).ToList();

        return Result.Success(dtos);
    }
}
