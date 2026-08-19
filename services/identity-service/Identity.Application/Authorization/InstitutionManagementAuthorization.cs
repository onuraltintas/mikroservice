using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Enums;

namespace Identity.Application.Authorization;

/// <summary>
/// Centralizes tenant checks for administrative institution operations. A
/// SystemAdmin has a global scope; every other administrator must have an
/// active membership in exactly the institution being accessed.
/// </summary>
public sealed class InstitutionManagementAuthorization
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IInstitutionRepository _institutionRepository;

    public InstitutionManagementAuthorization(
        ICurrentUserService currentUserService,
        IInstitutionRepository institutionRepository)
    {
        _currentUserService = currentUserService;
        _institutionRepository = institutionRepository;
    }

    public bool IsSystemAdministrator => _currentUserService.Roles.Any(role =>
        string.Equals(role, UserRole.SystemAdmin.ToString(), StringComparison.OrdinalIgnoreCase));

    public async Task<Result<InstitutionAccessScope>> ResolveScopeAsync(
        CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId is not { } userId)
        {
            return Result.Failure<InstitutionAccessScope>(
                Error.Unauthorized("Oturum açılmış kullanıcı bulunamadı."));
        }

        if (IsSystemAdministrator)
        {
            return Result.Success(new InstitutionAccessScope(null));
        }

        var institutionId = await _institutionRepository.GetPrimaryInstitutionIdByUserIdAsync(
            userId,
            cancellationToken);
        return institutionId.HasValue
            ? Result.Success(new InstitutionAccessScope(institutionId))
            : Result.Failure<InstitutionAccessScope>(
                Error.Forbidden("Kullanıcının erişebileceği aktif bir kurum bulunamadı."));
    }

    public async Task<Result> EnsureInstitutionAccessAsync(
        Guid institutionId,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveScopeAsync(cancellationToken);
        if (scope.IsFailure)
        {
            return Result.Failure(scope.Error);
        }

        return scope.Value.IsGlobal || scope.Value.InstitutionId == institutionId
            ? Result.Success()
            : Result.Failure(Error.Forbidden("Bu kurum için işlem yetkiniz yok."));
    }

    public Result EnsureSystemAdministrator()
    {
        return IsSystemAdministrator
            ? Result.Success()
            : Result.Failure(Error.Forbidden("Bu işlem yalnızca sistem yöneticisi tarafından yapılabilir."));
    }

    public async Task<Result> EnsureUserInCurrentInstitutionAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveScopeAsync(cancellationToken);
        if (scope.IsFailure)
        {
            return Result.Failure(scope.Error);
        }

        if (scope.Value.IsGlobal)
        {
            return Result.Success();
        }

        return await _institutionRepository.IsUserInInstitutionAsync(
            userId,
            scope.Value.InstitutionId!.Value,
            cancellationToken)
            ? Result.Success()
            : Result.Failure(Error.Forbidden("Hedef kullanıcı bu kurumun aktif üyesi değil."));
    }
}
