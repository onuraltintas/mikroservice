using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Application.LegalPages;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MediatR;
using MassTransit;
using EduPlatform.Shared.Contracts.Events.Identity;

namespace Identity.Application.Commands.RegisterStudent;

public class RegisterStudentCommandHandler : IRequestHandler<RegisterStudentCommand, Result<Guid>>
{
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IConfigurationService _configurationService;
    private readonly IRegistrationLegalConsentService _registrationLegalConsentService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRoleRepository _roleRepository;

    public RegisterStudentCommandHandler(
        IIdentityService identityService,
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        IConfigurationService configurationService,
        IRegistrationLegalConsentService registrationLegalConsentService,
        IPasswordHasher passwordHasher,
        IRoleRepository roleRepository)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _configurationService = configurationService;
        _registrationLegalConsentService = registrationLegalConsentService;
        _passwordHasher = passwordHasher;
        _roleRepository = roleRepository;
    }

    public async Task<Result<Guid>> Handle(RegisterStudentCommand request, CancellationToken cancellationToken)
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

        var identityResult = await _identityService.RegisterUserAsync(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            cancellationToken);

        if (identityResult.IsFailure)
        {
            if (product == PlatformProduct.Coaching && identityResult.Error.Code == "Identity.UserExists")
                return await JoinExistingAccountAsync(request, legalValidation.Value, cancellationToken);

            if (identityResult.Error.Code == "User.Duplicate")
            {
                // Check local DB if user exists but inactive (Soft Deleted)
                var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        
                if (existingUser != null
                    && !existingUser.IsActive
                    && existingUser.HasProductAccess(product))
                {
                    // Reactivate Case
                    existingUser.Activate();
                    _registrationLegalConsentService.TrackAcceptedDocuments(
                        existingUser.Id, product, legalValidation.Value, "password");
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
            
                    // Reactivate in System (This will enable user AND set emailVerified=true)
                    await _identityService.ActivateUserAsync(existingUser.Id, cancellationToken);
            
                    return Result.Success(existingUser.Id);
                }
            }

            return Result.Failure<Guid>(identityResult.Error);
        }

        var userId = identityResult.Value;

        // Assign Role
        var roleResult = await _identityService.AssignRoleForProductAsync(
            userId,
            Identity.Domain.Enums.UserRole.Student.ToString(),
            product,
            cancellationToken,
            UserProductAccessSource.SelfRegistration);
        if (roleResult.IsFailure)
        {
             await _identityService.DeleteUserAsync(userId, cancellationToken);
             return Result.Failure<Guid>(roleResult.Error);
        }

        try
        {
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

            // await _userRepository.AddAsync(user, cancellationToken); // REMOVED: User already exists
            if (product == PlatformProduct.Coaching)
            {
                var student = StudentProfile.Create(userId, request.FirstName, request.LastName, null, null);
                await _studentRepository.AddAsync(student, cancellationToken);
            }

            // Publish Event for Notification Service (Verification)
            await _publishEndpoint.Publish(new UserRegisteredEvent(
                userId,
                request.Email,
                request.FirstName,
                request.LastName,
                user?.EmailVerificationToken ?? "",
                Identity.Domain.Enums.UserRole.Student.ToString()
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

    private async Task<Result<Guid>> JoinExistingAccountAsync(
        RegisterStudentCommand request,
        IReadOnlyList<PlatformLegalPageDto> legalDocuments,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !_passwordHasher.VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt))
            return Result.Failure<Guid>(new Error("Auth.ExistingAccountPasswordRequired",
                "Bu e-posta ile mevcut bir hesabınız var. Koçluk'a katılmak için yeni şifre oluşturmak yerine mevcut hesabınızın şifresini girin."));

        if (!user.IsActive || !user.EmailConfirmed)
            return Result.Failure<Guid>(new Error("Auth.ExistingAccountNotEligible",
                "Koçluk'a katılmak için mevcut hesabınızın aktif ve e-posta adresinin doğrulanmış olması gerekir."));

        if (user.MfaEnabled)
            return Result.Failure<Guid>(new Error("Auth.ExistingAccountMfaRequired",
                "Bu hesap iki adımlı doğrulama kullanıyor. Koçluk katılımını doğrulanmış oturum üzerinden yöneticiyle tamamlayın."));

        // Registration must not reactivate revoked access or change an existing Coaching role.
        if (user.ProductAccesses.Any(access => access.Product == PlatformProduct.Coaching)
            || user.Roles.Any(role => role.Product == PlatformProduct.Coaching))
            return Result.Failure<Guid>(new Error("Identity.UserExists",
                "Bu hesabın Koçluk kaydı zaten var. Giriş yapın; erişim kapatılmışsa Koçluk yöneticisiyle iletişime geçin."));

        var role = await _roleRepository.GetByNameAsync("Student", cancellationToken);
        if (role is null || role.IsDeleted)
            return Result.Failure<Guid>(new Error("Identity.RoleNotFound", "Öğrenci kayıt rolü şu anda kullanılamıyor."));

        var profile = await _studentRepository.GetExistingForRegistrationAsync(user.Id, cancellationToken);
        if (profile is not null && (!profile.IsActive || profile.InstitutionId.HasValue))
            return Result.Failure<Guid>(new Error("Auth.ExistingAccountNotEligible",
                "Mevcut öğrenci profiliniz pasif veya bir kuruma bağlı. Koçluk katılımını yöneticinizle tamamlayın."));
        if (profile is null)
            await _studentRepository.AddAsync(StudentProfile.Create(user.Id, user.FirstName, user.LastName, null, null), cancellationToken);

        user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id, PlatformProduct.Coaching));
        if (user.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.SelfRegistration, user.Id, DateTimeOffset.UtcNow))
            _userRepository.TrackProductAccessIfNew(user, PlatformProduct.Coaching);
        _registrationLegalConsentService.TrackAcceptedDocuments(user.Id, PlatformProduct.Coaching, legalDocuments, "password");
        // Persist membership, profile and consent together; the existing identity is never deleted on failure.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(user.Id);
    }
}
