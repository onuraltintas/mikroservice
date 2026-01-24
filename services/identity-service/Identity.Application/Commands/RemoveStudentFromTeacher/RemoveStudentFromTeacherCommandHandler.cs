using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.RemoveStudentFromTeacher;

public class RemoveStudentFromTeacherCommandHandler : IRequestHandler<RemoveStudentFromTeacherCommand, Result>
{
    private readonly ITeacherRepository _teacherRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public RemoveStudentFromTeacherCommandHandler(
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

    public async Task<Result> Handle(RemoveStudentFromTeacherCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate: User must be authenticated
        if (_currentUserService.UserId == null)
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated"));

        var teacherUserId = _currentUserService.UserId.Value;

        // 2. Get Teacher Profile
        var teacher = await _teacherRepository.GetByUserIdAsync(teacherUserId, cancellationToken);
        if (teacher == null)
            return Result.Failure(new Error("Forbidden", "You are not a teacher"));

        // 3. Get Student (to ensure exists, or just use ID if we trust it)
        var student = await _studentRepository.GetByIdAsync(request.StudentId, cancellationToken);
        if (student == null)
            return Result.Failure(new Error("Student.NotFound", "Student not found"));

        // 4. Find Assignment
        var assignment = await _teacherRepository.GetAssignmentAsync(teacher.Id, student.Id, cancellationToken);
        if (assignment == null)
            return Result.Failure(new Error("Assignment.NotFound", "This student is not assigned to you"));

        // 5. End Assignment
        assignment.End();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
