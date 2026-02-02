using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;
using MassTransit;
using EduPlatform.Shared.Contracts.Events.Identity;

namespace Identity.Application.Commands.RegisterTeacher;

public class RegisterTeacherCommandHandler : IRequestHandler<RegisterTeacherCommand, Result<Guid>>
{
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _userRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IConfigurationService _configurationService;

    public RegisterTeacherCommandHandler(
        IIdentityService identityService,
        IUserRepository userRepository,
        ITeacherRepository teacherRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        IConfigurationService configurationService)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _teacherRepository = teacherRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _configurationService = configurationService;
    }

    public async Task<Result<Guid>> Handle(RegisterTeacherCommand request, CancellationToken cancellationToken)
    {
        // Global Registration Switch Check
        var allowRegistration = await _configurationService.GetConfigurationValueAsync("auth.allowregistration", cancellationToken);
        if (!string.Equals(allowRegistration, "true", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<Guid>(new Error("Identity.RegistrationDisabled", "Yeni kullanıcı kayıtları sistem yöneticisi tarafından geçici olarak durdurulmuştur."));
        }

        var identityResult = await _identityService.RegisterUserAsync(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            cancellationToken);

        if (identityResult.IsFailure)
        {
            return Result.Failure<Guid>(identityResult.Error);
        }

        var userId = identityResult.Value;

        // Assign Role
        await _identityService.AssignRoleAsync(userId, Identity.Domain.Enums.UserRole.Teacher.ToString(), cancellationToken);

        // Update Phone if needed
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user != null) 
        {
            if (request.Phone != null) user.SetPhoneNumber(request.Phone);
            user.GenerateEmailVerificationToken();
        }

        // Independent Teacher
        var teacher = TeacherProfile.Create(userId, request.FirstName, request.LastName, null, true);

        try
        {
            // await _userRepository.AddAsync(user, cancellationToken); // Removed
            await _teacherRepository.AddAsync(teacher, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Publish Event for Notification Service (Verification)
            await _publishEndpoint.Publish(new UserRegisteredEvent(
                userId,
                request.Email,
                request.FirstName,
                request.LastName,
                user?.EmailVerificationToken ?? ""
            ), cancellationToken);

            return Result.Success(teacher.Id);
        }
        catch (Exception ex)
        {
            await _identityService.DeleteUserAsync(userId, cancellationToken);
            return Result.Failure<Guid>(new Error("Registration.Failed", $"Database error: {ex.Message}"));
        }
    }
}
