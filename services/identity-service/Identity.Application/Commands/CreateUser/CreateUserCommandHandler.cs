using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using MassTransit;
using EduPlatform.Shared.Contracts.Events.Identity;
using Identity.Domain.Enums;
using EduPlatform.Shared.Kernel.Primitives;

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
    private readonly Microsoft.Extensions.Logging.ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(
        IIdentityService identityService,
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        ITeacherRepository teacherRepository,
        IInstitutionRepository institutionRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        Microsoft.Extensions.Logging.ILogger<CreateUserCommandHandler> logger)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _teacherRepository = teacherRepository;
        _institutionRepository = institutionRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<Result<CreateUserResponse>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // 0. Parse Role
        if (!Enum.TryParse<Identity.Domain.Enums.UserRole>(request.Role, true, out var userRole))
        {
            return Result.Failure<CreateUserResponse>(new Error("Validation.InvalidRole", $"Role '{request.Role}' is invalid."));
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

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 6. Publish Event
            try
            {
                await _publishEndpoint.Publish(new UserCreatedEvent(
                    userId,
                    request.Email,
                    request.FirstName,
                    request.LastName,
                    request.Role,
                    tempPassword,
                    DateTime.UtcNow
                ), cancellationToken);
            }
            catch (Exception ex)
            {
                // Log and continue. We don't want to fail the user creation just because MQ is down.
                // In a real system, we might write to an Outbox table here.
                _logger.LogWarning(ex, "Failed to publish UserCreatedEvent");
            }

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
