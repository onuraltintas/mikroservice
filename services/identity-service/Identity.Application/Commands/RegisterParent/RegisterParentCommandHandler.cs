using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;
using MassTransit;
using EduPlatform.Shared.Contracts.Events.Identity;

namespace Identity.Application.Commands.RegisterParent;

public class RegisterParentCommandHandler : IRequestHandler<RegisterParentCommand, Result<Guid>>
{
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _userRepository;
    private readonly IParentRepository _parentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IConfigurationService _configurationService;

    public RegisterParentCommandHandler(
        IIdentityService identityService,
        IUserRepository userRepository,
        IParentRepository parentRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        IConfigurationService configurationService)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _parentRepository = parentRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _configurationService = configurationService;
    }

    public async Task<Result<Guid>> Handle(RegisterParentCommand request, CancellationToken cancellationToken)
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
        var roleResult = await _identityService.AssignRoleAsync(userId, Identity.Domain.Enums.UserRole.Parent.ToString(), cancellationToken);
        if (roleResult.IsFailure)
        {
             await _identityService.DeleteUserAsync(userId, cancellationToken);
             return Result.Failure<Guid>(roleResult.Error);
        }

        // Create Parent Profile
        var parent = ParentProfile.Create(userId, request.FirstName, request.LastName);

        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user != null) 
            {
                if (request.PhoneNumber != null) user.SetPhoneNumber(request.PhoneNumber);
                user.GenerateEmailVerificationToken();
            }

            await _parentRepository.AddAsync(parent, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Publish Event for Notification Service (Verification)
            await _publishEndpoint.Publish(new UserRegisteredEvent(
                userId,
                request.Email,
                request.FirstName,
                request.LastName,
                user?.EmailVerificationToken ?? ""
            ), cancellationToken);

            return Result.Success(parent.Id);
        }
        catch (Exception ex)
        {
            await _identityService.DeleteUserAsync(userId, cancellationToken);
            return Result.Failure<Guid>(new Error("Registration.Failed", $"Database error: {ex.Message}"));
        }
    }
}
