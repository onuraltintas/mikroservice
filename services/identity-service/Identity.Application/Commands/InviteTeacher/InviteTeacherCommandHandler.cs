using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MediatR;
using MassTransit;

namespace Identity.Application.Commands.InviteTeacher;

public class InviteTeacherCommandHandler : IRequestHandler<InviteTeacherCommand, Result<Guid>>
{
    private readonly IInvitationRepository _invitationRepository;
    private readonly IInstitutionRepository _institutionRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPublishEndpoint _publishEndpoint;

    public InviteTeacherCommandHandler(
        IInvitationRepository invitationRepository,
        IInstitutionRepository institutionRepository,
        IUserRepository userRepository,
        ITeacherRepository teacherRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IPublishEndpoint publishEndpoint)
    {
        _invitationRepository = invitationRepository;
        _institutionRepository = institutionRepository;
        _userRepository = userRepository;
        _teacherRepository = teacherRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<Result<Guid>> Handle(InviteTeacherCommand request, CancellationToken cancellationToken)
    {
        // ... (validation logic remains same)

        // 1. Validate current user is institution admin
        if (_currentUserService.UserId == null)
        {
            return Result.Failure<Guid>(new Error("Auth.Unauthorized", "User is not authenticated"));
        }

        var adminUserId = _currentUserService.UserId.Value;
        var institutionId = await _institutionRepository.GetInstitutionIdByAdminIdAsync(adminUserId, cancellationToken);
        
        if (institutionId == null)
        {
            return Result.Failure<Guid>(new Error("InviteTeacher.Forbidden", "You are not an admin of any institution"));
        }

        // 3. Check if there's already a pending invitation
        var existingInvitations = await _invitationRepository.GetPendingByEmailAsync(request.TeacherEmail, cancellationToken);
        var duplicateInvitation = existingInvitations.FirstOrDefault(i => 
            i.Type == InvitationType.TeacherToInstitution && 
            i.InstitutionId == institutionId);
            
        if (duplicateInvitation != null)
        {
            return Result.Failure<Guid>(new Error("InviteTeacher.DuplicateInvitation", "An invitation has already been sent to this teacher"));
        }

        // 4. Create invitation
        var invitation = Invitation.Create(
            inviterId: adminUserId,
            inviteeEmail: request.TeacherEmail,
            type: InvitationType.TeacherToInstitution,
            institutionId: institutionId,
            message: request.Message
        );

        await _invitationRepository.AddAsync(invitation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Find invitee user id if exists
        var inviteeUser = await _userRepository.GetByEmailAsync(request.TeacherEmail, cancellationToken);

        // Publish Event
        var eventMessage = new EduPlatform.Shared.Contracts.Events.Identity.InvitationCreatedEvent(
            InvitationId: invitation.Id,
            InviterEmail: _currentUserService.Email ?? "system",
            InviteeEmail: request.TeacherEmail,
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
