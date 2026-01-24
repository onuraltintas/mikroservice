using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.RemoveStudentFromInstitution;

public class RemoveStudentFromInstitutionCommandHandler : IRequestHandler<RemoveStudentFromInstitutionCommand, Result>
{
    private readonly IInstitutionRepository _institutionRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public RemoveStudentFromInstitutionCommandHandler(
        IInstitutionRepository institutionRepository,
        IStudentRepository studentRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _institutionRepository = institutionRepository;
        _studentRepository = studentRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(RemoveStudentFromInstitutionCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate: User must be Institution Admin
        if (_currentUserService.UserId == null)
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated"));

        var adminUserId = _currentUserService.UserId.Value;
        var institutionId = await _institutionRepository.GetInstitutionIdByAdminIdAsync(adminUserId, cancellationToken);
        
        if (institutionId == null)
            return Result.Failure(new Error("Forbidden", "You are not an institution admin"));

        // 2. Get Student
        var student = await _studentRepository.GetByIdAsync(request.StudentId, cancellationToken);
        if (student == null)
        {
            return Result.Failure(new Error("Student.NotFound", "Student not found"));
        }

        // 3. Verify student belongs to this institution
        if (student.InstitutionId != institutionId)
        {
            return Result.Failure(new Error("RemoveStudent.Forbidden", "This student does not belong to your institution"));
        }

        // 4. Remove from Institution
        student.RemoveFromInstitution();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
