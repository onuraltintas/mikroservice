using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MediatR;
using MassTransit;
using EduPlatform.Shared.Contracts.Events.Identity;
using EduPlatform.Shared.Kernel.Primitives;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.Extensions.Logging;
using UserRoleType = Identity.Domain.Enums.UserRole;

namespace Identity.Application.Commands.CreateUser;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<CreateUserResponse>>
{
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(
        IIdentityService identityService,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        ICurrentUserService currentUserService,
        ILogger<CreateUserCommandHandler> logger)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<CreateUserResponse>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var roleName = request.Role.Trim();
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return Result.Failure<CreateUserResponse>(new Error("Validation.InvalidRole", "Rol seçimi zorunludur."));
        }

        // Built-in roles have profile and privilege rules. Custom roles are
        // validated by the identity service against the active role catalog.
        var hasBuiltInRole = Enum.TryParse<UserRoleType>(roleName, true, out var parsedRole)
            && Enum.IsDefined(typeof(UserRoleType), parsedRole);
        UserRoleType? userRole = hasBuiltInRole ? parsedRole : null;
        if (hasBuiltInRole)
            roleName = parsedRole.ToString();

        PlatformProduct[] products = userRole == UserRoleType.SystemAdmin ? []
            : request.Products ?? (request.Product is { } legacyProduct ? [legacyProduct] : []);
        if (userRole != UserRoleType.SystemAdmin && (products.Length == 0
            || products.Any(product => product is not (PlatformProduct.Coaching or PlatformProduct.SpeedReading))))
        {
            return Result.Failure<CreateUserResponse>(new Error("Auth.InvalidProduct", "Koçluk veya Hızlı Okuma servisi seçilmelidir."));
        }
        products = products.Distinct().Order().ToArray();
        PlatformProduct? product = products.Length == 0 ? null : products[0];

        if (userRole == UserRoleType.SystemAdmin
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
            roleName,
            product,
            request.PhoneNumber,
            cancellationToken);

        if (identityResult.IsFailure)
            return Result.Failure<CreateUserResponse>(identityResult.Error);

        var provisionedUser = identityResult.Value;
        var userId = provisionedUser.UserId;

        // 4. Create Profile based on Role
        try 
        {
            if (product.HasValue)
            {
                var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
                if (user is null)
                {
                    throw new InvalidOperationException("Provisioned user was not found in the Identity database.");
                }

                var roleId = user.Roles.Single(role => role.Product == product).RoleId;
                foreach (var selectedProductScope in products)
                {
                    if (selectedProductScope != product)
                        user.AddRole(new Identity.Domain.Entities.UserRole(userId, roleId, selectedProductScope));
                    if (user.GrantProductAccess(
                        selectedProductScope,
                        UserProductAccessSource.Admin,
                        _currentUserService.UserId,
                        DateTimeOffset.UtcNow))
                    {
                        _userRepository.TrackProductAccessIfNew(user, selectedProductScope);
                    }
                }
            }

            if (products.Contains(PlatformProduct.Coaching) && userRole is UserRoleType.Student or UserRoleType.Teacher)
                await _publishEndpoint.Publish(new EduPlatform.Shared.Contracts.Coaching.CoachingProfileProvisioningRequested(
                    userId, roleName, DateTime.UtcNow), cancellationToken);

            // 6. Queue the event in the EF bus outbox before the single commit.
            // If the database is unavailable, neither the access grant nor the event
            // is reported as successfully created.
            await Identity.Application.Services.AccountCreatedEvents.PublishAsync(_publishEndpoint, provisionedUser,
                request.Email, request.FirstName, request.LastName, roleName, product?.ToRouteValue(), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new CreateUserResponse(userId));
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "User profile provisioning failed for {UserId}.", userId);
             _unitOfWork.ClearTracking();
             using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
             try
             {
                 var cleanup = await _identityService.DeleteUserAsync(userId, cleanupTimeout.Token);
                 if (cleanup.IsFailure)
                     _logger.LogError("New account cleanup failed for {UserId}: {Code}", userId, cleanup.Error.Code);
             }
             catch (Exception cleanupException)
             {
                 _logger.LogError(cleanupException, "New account cleanup failed for {UserId}.", userId);
             }
             return Result.Failure<CreateUserResponse>(new Error("CreateUser.Failed", "Kullanıcı oluşturulamadı. Lütfen daha sonra tekrar deneyin."));
        }
    }
}
