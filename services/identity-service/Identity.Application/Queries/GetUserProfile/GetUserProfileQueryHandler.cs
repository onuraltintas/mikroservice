using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Authorization;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Queries.GetUserProfile;

public class GetUserProfileQueryHandler : IRequestHandler<GetUserProfileQuery, Result<UserProfileDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IInstitutionRepository _institutionRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetUserProfileQueryHandler(
        IUserRepository userRepository,
        ITeacherRepository teacherRepository,
        IStudentRepository studentRepository,
        IInstitutionRepository institutionRepository,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _institutionRepository = institutionRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<UserProfileDto>> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId is null)
        {
            return Result.Failure<UserProfileDto>(
                Error.Unauthorized("Oturum açılmış kullanıcı bulunamadı."));
        }

        Guid? institutionScope = null;
        if (currentUserId != request.UserId)
        {
            var institutionId = await _institutionRepository
                .GetPrimaryInstitutionIdByUserIdAsync(
                    currentUserId.Value,
                    request.Product ?? PlatformProduct.Coaching,
                    cancellationToken);
            var scope = InstitutionAccessScopeResolver.Resolve(
                currentUserId,
                _currentUserService.Roles,
                institutionId);

            if (scope.IsFailure)
            {
                return Result.Failure<UserProfileDto>(scope.Error);
            }

            institutionScope = scope.Value.IsGlobal ? null : scope.Value.InstitutionId;

            if (!scope.Value.IsGlobal && !await _institutionRepository.IsUserInInstitutionAsync(
                    request.UserId,
                    scope.Value.InstitutionId!.Value,
                    request.Product ?? PlatformProduct.Coaching,
                    cancellationToken))
            {
                return Result.Failure<UserProfileDto>(
                    Error.Forbidden("Bu kullanıcı aynı kurum kapsamında değil."));
            }
        }

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("User", request.UserId);
        }

        var isSystemAdmin = _currentUserService.Roles.Any(role =>
            string.Equals(
                role,
                Identity.Domain.Enums.UserRole.SystemAdmin.ToString(),
                StringComparison.OrdinalIgnoreCase));
        if (request.Product is { } product
            && !isSystemAdmin
            && !user.HasProductAccess(product))
        {
            return Result.Failure<UserProfileDto>(
                Error.Forbidden("Bu kullanıcı seçilen platforma kayıtlı değil."));
        }

        var visibleRoles = isSystemAdmin && request.Product is null
            ? user.Roles.ToArray()
            : user.GetRolesForProductScope(request.Product).ToArray();

        var profile = new UserProfileDto
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName ?? "Unknown", // Handle nulls safely if properties not forced yet
            LastName = user.LastName ?? "Unknown",
            FullName = $"{user.FirstName} {user.LastName}",
            PhoneNumber = user.PhoneNumber,
            LastLoginAt = user.LastLoginAt,
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            MfaEnabled = user.MfaEnabled,
            Role = visibleRoles.FirstOrDefault()?.Role?.Name ?? "Unknown",
            Roles = visibleRoles.Select(r => r.Role?.Name ?? "Unknown").ToList(),
            ProductRoles = visibleRoles
                .Select(userRole => new UserProductRoleDto(
                    userRole.Role?.Name ?? "Unknown",
                    userRole.Product?.ToRouteValue()))
                .ToList(),
            ProductAccesses = isSystemAdmin
                ? user.ProductAccesses
                .Select(access => new UserProductAccessDto(access.Product.ToRouteValue(), access.IsActive))
                .ToList()
                : request.Product is { } requestedProduct
                    ? user.ProductAccesses
                        .Where(access => access.Product == requestedProduct)
                        .Select(access => new UserProductAccessDto(access.Product.ToRouteValue(), access.IsActive))
                        .ToList()
                    : [],
            Permissions = visibleRoles
                .SelectMany(r => r.Role?.Permissions ?? new List<RolePermission>())
                .Where(permission => (!isSystemAdmin && request.Product is { } product)
                    ? PlatformPermissionScope.IsAllowed(permission.Permission, product)
                    : true)
                .Select(p => p.Permission)
                .Distinct()
                .ToList()
        };

        return Result.Success(profile);
    }
}
