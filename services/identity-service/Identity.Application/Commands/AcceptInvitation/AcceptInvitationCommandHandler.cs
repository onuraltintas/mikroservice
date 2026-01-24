using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.AcceptInvitation;

public class AcceptInvitationCommandHandler : IRequestHandler<AcceptInvitationCommand, Result>
{
    private readonly IInvitationRepository _invitationRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AcceptInvitationCommandHandler(
        IInvitationRepository invitationRepository,
        IUserRepository userRepository,
        ITeacherRepository teacherRepository,
        IStudentRepository studentRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _invitationRepository = invitationRepository;
        _userRepository = userRepository;
        _teacherRepository = teacherRepository;
        _studentRepository = studentRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate current user is authenticated
        if (_currentUserService.UserId == null)
        {
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated"));
        }

        var userId = _currentUserService.UserId.Value;
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

        // 5. Accept invitation and perform the assignment
        try
        {
            invitation.Accept(userId);

            // Perform different actions based on invitation type
            switch (invitation.Type)
            {
                case InvitationType.TeacherToInstitution:
                    await AssignTeacherToInstitution(userId, invitation.InstitutionId!.Value, cancellationToken);
                    break;

                case InvitationType.StudentToInstitution:
                    await AssignStudentToInstitution(userId, invitation.InstitutionId!.Value, cancellationToken);
                    break;

                case InvitationType.StudentToTeacher:
                    await AssignStudentToTeacher(userId, invitation.TeacherId!.Value, cancellationToken);
                    break;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("AcceptInvitation.Failed", ex.Message));
        }
    }

    private async Task AssignTeacherToInstitution(Guid teacherUserId, Guid institutionId, CancellationToken cancellationToken)
    {
        var teacher = await _teacherRepository.GetByUserIdAsync(teacherUserId, cancellationToken);
        if (teacher == null)
        {
            throw new InvalidOperationException("Teacher profile not found");
        }

        teacher.AssignToInstitution(institutionId);
    }

    private async Task AssignStudentToInstitution(Guid studentUserId, Guid institutionId, CancellationToken cancellationToken)
    {
        var student = await _studentRepository.GetByUserIdAsync(studentUserId, cancellationToken);
        if (student == null)
        {
            throw new InvalidOperationException("Student profile not found");
        }

        // Assign student to institution
        student.AssignToInstitution(institutionId);
    }

    private async Task AssignStudentToTeacher(Guid studentUserId, Guid teacherId, CancellationToken cancellationToken)
    {
        var student = await _studentRepository.GetByUserIdAsync(studentUserId, cancellationToken);
        
        if (student == null)
        {
            throw new InvalidOperationException("Student profile not found");
        }

        // teacherId parameter is Invitation.TeacherId which is TeacherProfile.Id
        
        var assignment = TeacherStudentAssignment.Create(
            teacherId: teacherId,
            studentId: student.Id,
            institutionId: student.InstitutionId, // If student belongs to an institution, link it
            subject: null,
            createdByUserId: studentUserId
        );
        
        await _teacherRepository.AddStudentAssignmentAsync(assignment, cancellationToken);
    }
}
