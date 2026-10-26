using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Application.LegalPages;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
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
    private readonly IRegistrationLegalConsentService _registrationLegalConsentService;

    public RegisterParentCommandHandler(
        IIdentityService identityService,
        IUserRepository userRepository,
        IParentRepository parentRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        IConfigurationService configurationService,
        IRegistrationLegalConsentService registrationLegalConsentService)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _parentRepository = parentRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _configurationService = configurationService;
        _registrationLegalConsentService = registrationLegalConsentService;
    }

    public async Task<Result<Guid>> Handle(RegisterParentCommand request, CancellationToken cancellationToken)
    {
        if (request.Product != PlatformProduct.Coaching)
        {
            return Result.Failure<Guid>(new Error("Auth.ProductRoleNotSupported", "Veli kaydı Koçluk platformunda yapılabilir."));
        }

        // Global Registration Switch Check
        var allowRegistration = await _configurationService.GetConfigurationValueAsync("auth.allowregistration", cancellationToken);
        if (!string.Equals(allowRegistration, "true", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<Guid>(new Error("Identity.RegistrationDisabled", "Yeni kullanıcı kayıtları sistem yöneticisi tarafından geçici olarak durdurulmuştur."));
        }

        var legalValidation = await _registrationLegalConsentService.ValidateAsync(
            PlatformProduct.Coaching,
            request.LegalAcceptances,
            cancellationToken);
        if (legalValidation.IsFailure)
            return Result.Failure<Guid>(legalValidation.Error);

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
        var roleResult = await _identityService.AssignRoleForProductAsync(
            userId,
            Identity.Domain.Enums.UserRole.Parent.ToString(),
            PlatformProduct.Coaching,
            cancellationToken,
            UserProductAccessSource.SelfRegistration);
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
            if (user is null)
            {
                await _identityService.DeleteUserAsync(userId, cancellationToken);
                return Result.Failure<Guid>(new Error("Registration.Failed", "Hesap kaydı tamamlanamadı."));
            }

            if (user.GrantProductAccess(
                PlatformProduct.Coaching,
                UserProductAccessSource.SelfRegistration,
                grantedByUserId: null,
                DateTimeOffset.UtcNow))
            {
                _userRepository.TrackProductAccessIfNew(user, PlatformProduct.Coaching);
            }
            if (request.PhoneNumber != null)
            {
                user.SetPhoneNumber(request.PhoneNumber);
            }
            user.GenerateEmailVerificationToken();
            _registrationLegalConsentService.TrackAcceptedDocuments(
                userId, PlatformProduct.Coaching, legalValidation.Value, "password");

            await _parentRepository.AddAsync(parent, cancellationToken);

            // Publish Event for Notification Service (Verification)
            await _publishEndpoint.Publish(new UserRegisteredEvent(
                userId,
                request.Email,
                request.FirstName,
                request.LastName,
                user?.EmailVerificationToken ?? "",
                Identity.Domain.Enums.UserRole.Parent.ToString()
            ), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(userId);
        }
        catch (Exception)
        {
            await _identityService.DeleteUserAsync(userId, cancellationToken);
            return Result.Failure<Guid>(new Error("Registration.Failed", "Registration could not be completed. Please try again later."));
        }
    }
}
