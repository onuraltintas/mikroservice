using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;

namespace Identity.Application.Queries.GetUserProfile;

public class GetUserProfileQueryHandler : IRequestHandler<GetUserProfileQuery, Result<UserProfileDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IInstitutionRepository _institutionRepository;

    public GetUserProfileQueryHandler(
        IUserRepository userRepository,
        ITeacherRepository teacherRepository,
        IStudentRepository studentRepository,
        IInstitutionRepository institutionRepository)
    {
        _userRepository = userRepository;
        _teacherRepository = teacherRepository;
        _studentRepository = studentRepository;
        _institutionRepository = institutionRepository;
    }

    public async Task<Result<UserProfileDto>> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("User", request.UserId);
        }

        var profile = new UserProfileDto
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName ?? "Unknown", // Handle nulls safely if properties not forced yet
            LastName = user.LastName ?? "Unknown",
            FullName = $"{user.FirstName} {user.LastName}",
            PhoneNumber = user.PhoneNumber,
            LastLoginAt = user.LastLoginAt,
            Role = user.Roles.FirstOrDefault()?.Role?.Name ?? "Unknown",
            Roles = user.Roles.Select(r => r.Role?.Name ?? "Unknown").ToList(),
            Permissions = user.Roles
                .SelectMany(r => r.Role?.Permissions ?? new List<RolePermission>())
                .Select(p => p.Permission)
                .Distinct()
                .ToList()
        };

        // Determine Role and Fetch Details
        var userRoles = user.Roles.Select(r => r.Role.Name).ToList();

        if (userRoles.Contains(Identity.Domain.Enums.UserRole.Teacher.ToString()))
        {
            var teacher = await _teacherRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            if (teacher != null)
            {
                profile.FullName = teacher.FullName; // Use profile name as it might be fresher
                profile.AvatarUrl = teacher.AvatarUrl;
                profile.TeacherDetails = new TeacherDetailsDto
                {
                    Title = teacher.Title,
                    Subjects = teacher.Subjects.ToArray(),
                    InstitutionId = teacher.InstitutionId,
                    InstitutionName = teacher.Institution?.Name
                };
            }
        }
        else if (userRoles.Contains(Identity.Domain.Enums.UserRole.Student.ToString()))
        {
            var student = await _studentRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            if (student != null)
            {
                profile.FullName = student.FullName;
                profile.AvatarUrl = student.AvatarUrl;
                profile.StudentDetails = new StudentDetailsDto
                {
                    GradeLevel = student.GradeLevel,
                    StudentNumber = null, // Not persisted yet
                    InstitutionId = student.InstitutionId,
                    InstitutionName = student.Institution?.Name,
                    BirthDate = student.BirthDate,
                    LearningStyle = student.LearningStyle.ToString()
                };
            }
        }
        else if (userRoles.Contains(Identity.Domain.Enums.UserRole.InstitutionAdmin.ToString()) || 
                 userRoles.Contains(Identity.Domain.Enums.UserRole.InstitutionOwner.ToString()))
        {
            // For admins, maybe fetch Institution details
            var institutionId = await _institutionRepository.GetInstitutionIdByAdminIdAsync(request.UserId, cancellationToken);
            // Can enrich further if needed
        }

        return Result.Success(profile);
    }
}
