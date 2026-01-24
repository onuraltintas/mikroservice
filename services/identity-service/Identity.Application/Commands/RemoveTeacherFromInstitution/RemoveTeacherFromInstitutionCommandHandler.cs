using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.RemoveTeacherFromInstitution;

public class RemoveTeacherFromInstitutionCommandHandler : IRequestHandler<RemoveTeacherFromInstitutionCommand, Result>
{
    private readonly IInstitutionRepository _institutionRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public RemoveTeacherFromInstitutionCommandHandler(
        IInstitutionRepository institutionRepository,
        ITeacherRepository teacherRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _institutionRepository = institutionRepository;
        _teacherRepository = teacherRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(RemoveTeacherFromInstitutionCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate: User must be Institution Admin
        if (_currentUserService.UserId == null)
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated"));

        var adminUserId = _currentUserService.UserId.Value;
        var institutionId = await _institutionRepository.GetInstitutionIdByAdminIdAsync(adminUserId, cancellationToken);
        
        if (institutionId == null)
            return Result.Failure(new Error("Forbidden", "You are not an institution admin"));

        if (institutionId == null)
            return Result.Failure(new Error("Forbidden", "You are not an institution admin"));

        // 2. Get Teacher
        var teacher = await _teacherRepository.GetByIdAsync(request.TeacherId, cancellationToken);
        if (teacher == null)
        {
            return Result.Failure(new Error("Teacher.NotFound", "Teacher not found"));
        }

        // 3. Verify teacher belongs to this institution
        if (teacher.InstitutionId != institutionId)
        {
            return Result.Failure(new Error("RemoveTeacher.Forbidden", "This teacher does not belong to your institution"));
        }

        // 4. Remove from Institution
        teacher.RemoveFromInstitution();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
