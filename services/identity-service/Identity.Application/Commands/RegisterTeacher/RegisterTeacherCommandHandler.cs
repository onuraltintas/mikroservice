using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Application.LegalPages;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
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
    private readonly IRegistrationLegalConsentService _registrationLegalConsentService;

    public RegisterTeacherCommandHandler(
        IIdentityService identityService,
        IUserRepository userRepository,
        ITeacherRepository teacherRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        IConfigurationService configurationService,
        IRegistrationLegalConsentService registrationLegalConsentService)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _teacherRepository = teacherRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _configurationService = configurationService;
        _registrationLegalConsentService = registrationLegalConsentService;
    }

    public async Task<Result<Guid>> Handle(RegisterTeacherCommand request, CancellationToken cancellationToken)
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
            return Result.Failure<Guid>(identityResult.Error);
        }

        var userId = identityResult.Value;

        // Assign Role
        var roleResult = await _identityService.AssignRoleForProductAsync(
            userId,
            Identity.Domain.Enums.UserRole.Teacher.ToString(),
            product,
            cancellationToken,
            UserProductAccessSource.SelfRegistration);
        if (roleResult.IsFailure)
        {
            await _identityService.DeleteUserAsync(userId, cancellationToken);
            return Result.Failure<Guid>(roleResult.Error);
        }

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

        try
        {
            if (product == PlatformProduct.Coaching)
            {
                var teacher = TeacherProfile.Create(userId, request.FirstName, request.LastName, null, true);
                await _teacherRepository.AddAsync(teacher, cancellationToken);
            }

            // Publish Event for Notification Service (Verification)
            await _publishEndpoint.Publish(new UserRegisteredEvent(
                userId,
                request.Email,
                request.FirstName,
                request.LastName,
                user?.EmailVerificationToken ?? "",
                Identity.Domain.Enums.UserRole.Teacher.ToString()
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
