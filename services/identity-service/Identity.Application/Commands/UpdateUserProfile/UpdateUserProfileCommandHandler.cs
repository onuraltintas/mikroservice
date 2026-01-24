using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;

namespace Identity.Application.Commands.UpdateUserProfile;

public class UpdateUserProfileCommandHandler : IRequestHandler<UpdateUserProfileCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateUserProfileCommandHandler(
        IUserRepository userRepository,
        ITeacherRepository teacherRepository,
        IStudentRepository studentRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _teacherRepository = teacherRepository;
        _studentRepository = studentRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId == null)
        {
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated"));
        }

        var userId = _currentUserService.UserId.Value;
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        
        if (user == null)
        {
            return Result.Failure(new Error("User.NotFound", "User not found"));
        }

        // 1. Update Core User Info
        if (!string.IsNullOrWhiteSpace(request.FirstName) || !string.IsNullOrWhiteSpace(request.LastName))
        {
            user.UpdateName(
                request.FirstName ?? user.FirstName, 
                request.LastName ?? user.LastName
            );
        }

        if (request.PhoneNumber != null)
        {
            user.SetPhoneNumber(request.PhoneNumber);
        }

        // 2. Update Profile Specifics
        var userRole = user.Roles.FirstOrDefault()?.Role;

        if (userRole == Identity.Domain.Enums.UserRole.Teacher)
        {
            var teacher = await _teacherRepository.GetByUserIdAsync(userId, cancellationToken);
            if (teacher != null)
            {
                teacher.UpdatePersonalInfo(
                    request.FirstName, 
                    request.LastName, 
                    title: request.Title
                );

                if (request.Bio != null) teacher.SetBio(request.Bio);
                if (request.AvatarUrl != null) teacher.SetAvatar(request.AvatarUrl);

                if (request.Subjects != null)
                {
                    teacher.SetSubjects(request.Subjects);
                }
            }
        }
        else if (userRole == Identity.Domain.Enums.UserRole.Student)
        {
            var student = await _studentRepository.GetByUserIdAsync(userId, cancellationToken);
            if (student != null)
            {
                student.UpdatePersonalInfo(
                    request.FirstName, 
                    request.LastName,
                    birthDate: request.BirthDate
                );
                
                if (request.LearningStyle.HasValue)
                {
                    student.SetLearningPreferences(style: request.LearningStyle);
                }

                 if (request.GradeLevel.HasValue)
                 {
                     student.UpdateEducationInfo(gradeLevel: request.GradeLevel);
                 }

                 if (request.Bio != null) student.SetBio(request.Bio);
                 if (request.AvatarUrl != null) student.SetAvatar(request.AvatarUrl);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
