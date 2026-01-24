using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MediatR;

using MassTransit;

namespace Identity.Application.Commands.AssignStudent;

public class AssignStudentCommandHandler : IRequestHandler<AssignStudentCommand, Result<Guid>>
{
    private readonly IInvitationRepository _invitationRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IUserRepository _userRepository; // Added
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPublishEndpoint _publishEndpoint;

    public AssignStudentCommandHandler(
        IInvitationRepository invitationRepository,
        ITeacherRepository teacherRepository,
        IUserRepository userRepository, // Added
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IPublishEndpoint publishEndpoint)
    {
        _invitationRepository = invitationRepository;
        _teacherRepository = teacherRepository;
        _userRepository = userRepository; // Added
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<Result<Guid>> Handle(AssignStudentCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate current user is a teacher
        if (_currentUserService.UserId == null)
        {
            return Result.Failure<Guid>(new Error("Auth.Unauthorized", "User is not authenticated"));
        }

        var teacherUserId = _currentUserService.UserId.Value;
        var teacher = await _teacherRepository.GetByUserIdAsync(teacherUserId, cancellationToken);
        
        if (teacher == null)
        {
            return Result.Failure<Guid>(new Error("AssignStudent.Forbidden", "You are not a teacher"));
        }

        // 2. Check if there's already a pending invitation
        var existingInvitations = await _invitationRepository.GetPendingByEmailAsync(request.StudentEmail, cancellationToken);
        var duplicateInvitation = existingInvitations.FirstOrDefault(i => 
            i.Type == InvitationType.StudentToTeacher && 
            i.TeacherId == teacher.Id);
            
        if (duplicateInvitation != null)
        {
            return Result.Failure<Guid>(new Error("AssignStudent.DuplicateInvitation", "An invitation has already been sent to this student"));
        }

        // 3. Create invitation
        var invitation = Invitation.Create(
            inviterId: teacherUserId,
            inviteeEmail: request.StudentEmail,
            type: InvitationType.StudentToTeacher,
            teacherId: teacher.Id,
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
