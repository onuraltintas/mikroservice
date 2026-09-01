using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;

namespace Identity.Application.Commands.UpdateUser;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStudentRepository _studentRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IInstitutionRepository _institutionRepository;

    public UpdateUserCommandHandler(
        IUserRepository userRepository,
        IIdentityService identityService,
        IUnitOfWork unitOfWork,
        IStudentRepository studentRepository,
        ITeacherRepository teacherRepository,
        IInstitutionRepository institutionRepository)
    {
        _userRepository = userRepository;
        _identityService = identityService;
        _unitOfWork = unitOfWork;
        _studentRepository = studentRepository;
        _teacherRepository = teacherRepository;
        _institutionRepository = institutionRepository;
    }

    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        
        if (user == null)
        {
            return Result.Failure(new Error("User.NotFound", "User not found"));
        }

        // 1. Update Database
        user.UpdateName(request.FirstName, request.LastName);
        if (request.PhoneNumber != null)
        {
            user.SetPhoneNumber(request.PhoneNumber);
        }

        if (request.UpdateRoleProfile)
        {
            var institution = request.InstitutionId.HasValue
                ? await _institutionRepository.GetByIdAsync(request.InstitutionId.Value, cancellationToken)
                : null;

            if (request.InstitutionId.HasValue && (institution is null || !institution.IsActive))
            {
                return Result.Failure(new Error("Institution.NotFound", "Aktif kurum bulunamadı."));
            }

            var roles = user.Roles
                .Where(userRole => userRole.Role is not null && !userRole.Role.IsDeleted)
                .Select(userRole => userRole.Role.Name);

            if (roles.Any(role => string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase)))
            {
                var teacher = await _teacherRepository.GetByUserIdAsync(request.UserId, cancellationToken);
                if (teacher is null)
                {
                    teacher = TeacherProfile.Create(
                        request.UserId,
                        request.FirstName,
                        request.LastName,
                        request.InstitutionId);
                    await _teacherRepository.AddAsync(teacher, cancellationToken);
                }

                teacher.UpdatePersonalInfo(
                    request.FirstName,
                    request.LastName,
                    request.TeacherTitle,
                    request.TeacherExperienceYears);
                if (request.TeacherSubjects is not null) teacher.SetSubjects(request.TeacherSubjects);
                if (request.Bio is not null) teacher.SetBio(request.Bio);
                if (request.AvatarUrl is not null) teacher.SetAvatar(request.AvatarUrl);
                if (request.InstitutionId.HasValue) teacher.AssignToInstitution(request.InstitutionId.Value);
                else teacher.RemoveFromInstitution();
            }
            else if (roles.Any(role => string.Equals(role, "Student", StringComparison.OrdinalIgnoreCase)))
            {
                var student = await _studentRepository.GetByUserIdAsync(request.UserId, cancellationToken);
                if (student is null)
                {
                    student = StudentProfile.Create(
                        request.UserId,
                        request.FirstName,
                        request.LastName,
                        request.InstitutionId);
                    await _studentRepository.AddAsync(student, cancellationToken);
                }

                student.UpdatePersonalInfo(
                    request.FirstName,
                    request.LastName,
                    request.StudentBirthDate);
                student.UpdateEducationInfo(request.StudentGradeLevel);
                student.SetLearningPreferences(request.StudentLearningStyle);
                if (request.Bio is not null) student.SetBio(request.Bio);
                if (request.AvatarUrl is not null) student.SetAvatar(request.AvatarUrl);
                if (request.InstitutionId.HasValue) student.AssignToInstitution(request.InstitutionId.Value);
                else student.RemoveFromInstitution();
            }
        }

        // 2. Save Changes
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
