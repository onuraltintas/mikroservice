using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;

namespace Identity.Application.Commands.UpdateInstitutionStudent;

public sealed class UpdateInstitutionStudentCommandHandler : IRequestHandler<UpdateInstitutionStudentCommand, Result>
{
    private readonly IInstitutionRepository _institutionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateInstitutionStudentCommandHandler(
        IInstitutionRepository institutionRepository,
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        ITeacherRepository teacherRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _institutionRepository = institutionRepository;
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _teacherRepository = teacherRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(UpdateInstitutionStudentCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId is not { } adminUserId)
        {
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated"));
        }

        if (request.GradeLevel is < 1 or > 12)
        {
            return Result.Failure(new Error("Student.InvalidGradeLevel", "Grade level must be between 1 and 12"));
        }

        var institutionId = await _institutionRepository.GetInstitutionIdByAdminIdAsync(adminUserId, cancellationToken);
        if (institutionId is null)
        {
            return Result.Failure(new Error("Institution.Forbidden", "You are not an administrator of an institution"));
        }

        var student = await _studentRepository.GetByUserIdAsync(request.StudentUserId, institutionId, cancellationToken);
        var user = await _userRepository.GetByIdAsync(request.StudentUserId, cancellationToken);
        if (student is null || user is null)
        {
            return Result.Failure(new Error("Student.NotFound", "Student not found"));
        }

        TeacherProfile? teacher = null;
        if (request.TeacherUserId is { } teacherUserId)
        {
            teacher = await _teacherRepository.GetByUserIdAsync(teacherUserId, institutionId, cancellationToken);
            if (teacher is null)
            {
                return Result.Failure(new Error("Teacher.NotFound", "Selected teacher does not belong to this institution"));
            }
        }

        student.UpdatePersonalInfo(request.FirstName.Trim(), request.LastName.Trim());
        student.UpdateEducationInfo(request.GradeLevel);
        user.UpdateName(request.FirstName.Trim(), request.LastName.Trim());

        var activeAssignments = await _teacherRepository.GetActiveAssignmentsForStudentAsync(
            student.Id,
            institutionId.Value,
            cancellationToken);
        foreach (var assignment in activeAssignments.Where(assignment => teacher is null || assignment.TeacherId != teacher.Id))
        {
            assignment.End();
        }

        if (teacher is not null && activeAssignments.All(assignment => assignment.TeacherId != teacher.Id))
        {
            await _teacherRepository.AddStudentAssignmentAsync(
                TeacherStudentAssignment.Create(teacher.Id, student.Id, institutionId, createdByUserId: adminUserId),
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
