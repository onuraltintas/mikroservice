using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Application.LegalPages;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MediatR;
using MassTransit;
using EduPlatform.Shared.Contracts.Events.Identity;

namespace Identity.Application.Commands.RegisterInstitution;

public class RegisterInstitutionCommandHandler : IRequestHandler<RegisterInstitutionCommand, Result<Guid>>
{
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _userRepository;
    private readonly IInstitutionRepository _institutionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IConfigurationService _configurationService;
    private readonly ILocationRepository _locationRepository;
    private readonly IRegistrationLegalConsentService _registrationLegalConsentService;

    public RegisterInstitutionCommandHandler(
        IIdentityService identityService,
        IUserRepository userRepository,
        IInstitutionRepository institutionRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        IConfigurationService configurationService,
        ILocationRepository locationRepository,
        IRegistrationLegalConsentService registrationLegalConsentService)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _institutionRepository = institutionRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _configurationService = configurationService;
        _locationRepository = locationRepository;
        _registrationLegalConsentService = registrationLegalConsentService;
    }

    public async Task<Result<Guid>> Handle(RegisterInstitutionCommand request, CancellationToken cancellationToken)
    {
        if (request.Product is not { } product || !Enum.IsDefined(product))
        {
            return Result.Failure<Guid>(new Error("Auth.ProductRequired", "Kayıt yapılacak platform belirtilmelidir."));
        }

        // Global Registration Switch Check
        var allowRegistration = await _configurationService.GetConfigurationValueAsync("auth.allowregistration", cancellationToken);
        if (!string.Equals(allowRegistration, "true", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<Guid>(new Error("Identity.RegistrationDisabled", "Yeni kullanıcı kayıtları sistem yöneticisi tarafından geçici olarak durdurulmuştur."));
        }

        var legalValidation = await _registrationLegalConsentService.ValidateAsync(
            product,
            request.LegalAcceptances,
            cancellationToken);
        if (legalValidation.IsFailure)
            return Result.Failure<Guid>(legalValidation.Error);

        var location = await _locationRepository.GetLocationAsync(
            request.ProvinceId,
            request.DistrictId,
            cancellationToken);
        if (location is null)
        {
            return Result.Failure<Guid>(new Error(
                "Institution.InvalidLocation",
                "Seçilen il ve ilçe eşleşmiyor."));
        }

        // 1. Create User in Keycloak
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

        // 2. Create Domain Entities
        // 2. Assign Roles
        var adminRoleResult = await _identityService.AssignRoleForProductAsync(
            userId,
            Identity.Domain.Enums.UserRole.InstitutionAdmin.ToString(),
            product,
            cancellationToken,
            UserProductAccessSource.SelfRegistration);
        var ownerRoleResult = await _identityService.AssignRoleForProductAsync(
            userId,
            Identity.Domain.Enums.UserRole.InstitutionOwner.ToString(),
            product,
            cancellationToken,
            UserProductAccessSource.SelfRegistration);
        if (adminRoleResult.IsFailure || ownerRoleResult.IsFailure)
        {
            await _identityService.DeleteUserAsync(userId, cancellationToken);
            return Result.Failure<Guid>(adminRoleResult.IsFailure ? adminRoleResult.Error : ownerRoleResult.Error);
        }

        // 3. Create Domain Entities
        // Update Phone if needed
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            await _identityService.DeleteUserAsync(userId, cancellationToken);
            return Result.Failure<Guid>(new Error("Registration.Failed", "Hesap kaydı tamamlanamadı."));
        }

        if (user.GrantProductAccess(
            product,
            UserProductAccessSource.SelfRegistration,
            grantedByUserId: null,
            DateTimeOffset.UtcNow))
        {
            _userRepository.TrackProductAccessIfNew(user, product);
        }
        if (request.Phone != null)
        {
            user.SetPhoneNumber(request.Phone);
        }
        user.GenerateEmailVerificationToken();
        _registrationLegalConsentService.TrackAcceptedDocuments(
            userId, product, legalValidation.Value, "password");

        var institution = Institution.Create(
            request.InstitutionName,
            request.InstitutionType,
            email: request.Email);
        institution.SetLocation(location.Value.Province, location.Value.District);

        var admin = InstitutionAdmin.Create(
            userId,
            institution.Id,
            InstitutionAdminRole.Owner,
            product);

        // 4. Save to Database (Transactional)
        try
        {
            // await _userRepository.AddAsync(user, cancellationToken); // Removed
            await _institutionRepository.AddAsync(institution, cancellationToken);
            await _institutionRepository.AddAdminAsync(admin, cancellationToken);

            // Publish Event for Notification Service (Verification)
            await _publishEndpoint.Publish(new UserRegisteredEvent(
                userId,
                request.Email,
                request.FirstName,
                request.LastName,
                user?.EmailVerificationToken ?? "",
                Identity.Domain.Enums.UserRole.InstitutionOwner.ToString()
            ), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(userId);
        }
        catch (Exception)
        {
            // 5. Compensating Transaction: Delete User from Keycloak
            await _identityService.DeleteUserAsync(userId, cancellationToken);
            
            return Result.Failure<Guid>(new Error("Registration.Failed", "Registration could not be completed. Please try again later."));
        }
    }
}
