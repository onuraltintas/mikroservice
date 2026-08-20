using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;
using MassTransit;
using EduPlatform.Shared.Contracts.Events.Identity;
using Identity.Domain.Enums;
using EduPlatform.Shared.Kernel.Primitives;
using EduPlatform.Shared.Security.Interfaces;

namespace Identity.Application.Commands.CreateUser;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<CreateUserResponse>>
{
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IInstitutionRepository _institutionRepository; 
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ICurrentUserService _currentUserService;

    public CreateUserCommandHandler(
        IIdentityService identityService,
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        ITeacherRepository teacherRepository,
        IInstitutionRepository institutionRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        ICurrentUserService currentUserService)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _teacherRepository = teacherRepository;
        _institutionRepository = institutionRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _currentUserService = currentUserService;
    }

    public async Task<Result<CreateUserResponse>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // 0. Parse Role
        if (!Enum.TryParse<Identity.Domain.Enums.UserRole>(request.Role, true, out var userRole))
        {
            return Result.Failure<CreateUserResponse>(new Error("Validation.InvalidRole", $"Role '{request.Role}' is invalid."));
        }

        if (userRole == Identity.Domain.Enums.UserRole.SystemAdmin
            && !_currentUserService.Roles.Any(role =>
                string.Equals(role, Identity.Domain.Enums.UserRole.SystemAdmin.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<CreateUserResponse>(Error.Forbidden(
                "SystemAdmin kullanıcısı yalnızca mevcut bir SystemAdmin tarafından oluşturulabilir."));
        }

        // 1. Check local DB uniqueness
        var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser != null)
            return Result.Failure<CreateUserResponse>(new Error("User.Exists", "Bu e-posta adresi zaten kayıtlı."));

        // 2. Create in System (User + Role + Password) - ATOMIC
        var identityResult = await _identityService.RegisterUserWithRoleAsync(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Role,
            request.PhoneNumber,
            cancellationToken);

        if (identityResult.IsFailure)
            return Result.Failure<CreateUserResponse>(identityResult.Error);

        var (userId, tempPassword) = identityResult.Value;

        // 4. Create Profile based on Role
        try 
        {
            switch (userRole)
            {
                case Identity.Domain.Enums.UserRole.Student:
                    var student = StudentProfile.Create(userId, request.FirstName, request.LastName);
                    await _studentRepository.AddAsync(student, cancellationToken);
                    break;

                case Identity.Domain.Enums.UserRole.Teacher:
                    var teacher = TeacherProfile.Create(userId, request.FirstName, request.LastName);
                    await _teacherRepository.AddAsync(teacher, cancellationToken);
                    break; 
            }

            // 6. Queue the event in the EF bus outbox before the single commit.
            // If the database is unavailable, neither the profile nor the event
            // is reported as successfully created.
            await _publishEndpoint.Publish(new UserCreatedEvent(
                userId,
                request.Email,
                request.FirstName,
                request.LastName,
                request.Role,
                tempPassword,
                DateTime.UtcNow
            ), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new CreateUserResponse(userId, tempPassword));
        }
        catch (Exception ex)
        {
             // Cleanup if profile creation fails? 
             await _identityService.DeleteUserAsync(userId, cancellationToken);
             return Result.Failure<CreateUserResponse>(new Error("CreateUser.Failed", $"Database error: {ex.Message}"));
        }
    }
}
