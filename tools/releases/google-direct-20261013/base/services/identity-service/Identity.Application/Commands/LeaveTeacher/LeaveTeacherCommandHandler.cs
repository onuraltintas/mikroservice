using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.LeaveTeacher;

public class LeaveTeacherCommandHandler : IRequestHandler<LeaveTeacherCommand, Result>
{
    private readonly IStudentRepository _studentRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public LeaveTeacherCommandHandler(
        IStudentRepository studentRepository,
        ITeacherRepository teacherRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _studentRepository = studentRepository;
        _teacherRepository = teacherRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(LeaveTeacherCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate: User must be authenticated
        if (_currentUserService.UserId == null)
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated"));

        var studentUserId = _currentUserService.UserId.Value;

        // 2. Get Student Profile
        var student = await _studentRepository.GetByUserIdAsync(studentUserId, cancellationToken);
        if (student == null)
            return Result.Failure(new Error("Forbidden", "You are not a student"));

        // 3. Get Teacher (Optional: Verify teacher exists)
        var teacher = await _teacherRepository.GetByIdAsync(request.TeacherId, cancellationToken);
        if (teacher == null)
            return Result.Failure(new Error("Teacher.NotFound", "Teacher not found"));

        // 4. Find Assignment
        var assignment = await _teacherRepository.GetAssignmentAsync(teacher.Id, student.Id, cancellationToken);
        if (assignment == null)
            return Result.Failure(new Error("Assignment.NotFound", "You are not assigned to this teacher"));

        // 5. End Assignment
        assignment.End();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
