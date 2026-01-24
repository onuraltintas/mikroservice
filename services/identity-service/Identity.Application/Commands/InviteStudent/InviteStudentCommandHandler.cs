using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MediatR;

using MassTransit;

namespace Identity.Application.Commands.InviteStudent;

public class InviteStudentCommandHandler : IRequestHandler<InviteStudentCommand, Result<Guid>>
{
    private readonly IInvitationRepository _invitationRepository;
    private readonly IInstitutionRepository _institutionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPublishEndpoint _publishEndpoint;

    public InviteStudentCommandHandler(
        IInvitationRepository invitationRepository,
        IInstitutionRepository institutionRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IPublishEndpoint publishEndpoint)
    {
        _invitationRepository = invitationRepository;
        _institutionRepository = institutionRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<Result<Guid>> Handle(InviteStudentCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate current user is institution admin
        if (_currentUserService.UserId == null)
        {
            return Result.Failure<Guid>(new Error("Auth.Unauthorized", "User is not authenticated"));
        }

        var adminUserId = _currentUserService.UserId.Value;
        var institutionId = await _institutionRepository.GetInstitutionIdByAdminIdAsync(adminUserId, cancellationToken);
        
        if (institutionId == null)
        {
            return Result.Failure<Guid>(new Error("InviteStudent.Forbidden", "You are not an admin of any institution"));
        }

        // 2. Check if there's already a pending invitation
        var existingInvitations = await _invitationRepository.GetPendingByEmailAsync(request.StudentEmail, cancellationToken);
        var duplicateInvitation = existingInvitations.FirstOrDefault(i => 
            i.Type == InvitationType.StudentToInstitution && 
            i.InstitutionId == institutionId);
            
        if (duplicateInvitation != null)
        {
            return Result.Failure<Guid>(new Error("InviteStudent.DuplicateInvitation", "An invitation has already been sent to this student"));
        }

        // 3. Create invitation
        var invitation = Invitation.Create(
            inviterId: adminUserId,
            inviteeEmail: request.StudentEmail,
            type: InvitationType.StudentToInstitution,
            institutionId: institutionId,
            message: request.Message
        );

        await _invitationRepository.AddAsync(invitation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Find invitee user id if exists
        var inviteeUser = await _userRepository.GetByEmailAsync(request.StudentEmail, cancellationToken);

        // Publish Event
        var eventMessage = new EduPlatform.Shared.Contracts.Events.Identity.InvitationCreatedEvent(
            InvitationId: invitation.Id,
            InviterEmail: _currentUserService.Email ?? "system",
            InviteeEmail: request.StudentEmail,
            InviteeId: inviteeUser?.Id,
            InvitationType: invitation.Type.ToString(),
            Message: request.Message,
            Link: $"http://localhost:3000/accept-invitation?id={invitation.Id}",
            CreatedAt: invitation.CreatedAt
        );

        await _publishEndpoint.Publish(eventMessage, cancellationToken);

        return Result.Success(invitation.Id);
    }
}
