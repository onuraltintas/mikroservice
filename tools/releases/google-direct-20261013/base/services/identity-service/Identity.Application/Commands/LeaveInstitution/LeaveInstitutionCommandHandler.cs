using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.LeaveInstitution;

public class LeaveInstitutionCommandHandler : IRequestHandler<LeaveInstitutionCommand, Result>
{
    private readonly ITeacherRepository _teacherRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public LeaveInstitutionCommandHandler(
        ITeacherRepository teacherRepository,
        IStudentRepository studentRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _teacherRepository = teacherRepository;
        _studentRepository = studentRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(LeaveInstitutionCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate: User must be authenticated
        if (_currentUserService.UserId == null)
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated"));

        var userId = _currentUserService.UserId.Value;

        // 2. Determine Role (Teacher or Student)
        // We'll try to find both. A user is typically one or the other.
        
        var teacher = await _teacherRepository.GetByUserIdAsync(userId, cancellationToken);
        if (teacher != null)
        {
            if (teacher.InstitutionId == null)
                return Result.Failure(new Error("LeaveInstitution.NotAssigned", "You are not assigned to any institution"));

            teacher.RemoveFromInstitution();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        var student = await _studentRepository.GetByUserIdAsync(userId, cancellationToken);
        if (student != null)
        {
            if (student.InstitutionId == null)
                return Result.Failure(new Error("LeaveInstitution.NotAssigned", "You are not assigned to any institution"));

            student.RemoveFromInstitution();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        return Result.Failure(new Error("LeaveInstitution.NotFound", "User profile not found"));
    }
}
