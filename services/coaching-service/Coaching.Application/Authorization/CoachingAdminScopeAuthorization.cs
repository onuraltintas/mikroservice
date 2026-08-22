using Coaching.Application.Interfaces;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;

namespace Coaching.Application.Authorization;

public interface ICoachingAdminScopeAuthorization
{
    Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken cancellationToken);
}

public sealed record CoachingAdminScope(
    bool IsGlobal,
    Guid? InstitutionId,
    IReadOnlyCollection<Guid>? StudentIds = null)
{
    public static CoachingAdminScope Global { get; } = new(true, null, null);
}

/// <summary>
/// Resolves the authenticated administrator's coaching scope. Identity remains
/// the source of truth for active institution membership; Coaching never trusts
/// a client-supplied tenant id for authorization.
/// </summary>
public sealed class CoachingAdminScopeAuthorization : ICoachingAdminScopeAuthorization
{
    private readonly ICurrentUserService _currentUser;
    private readonly ICoachingIdentityAuthorizationClient _identityClient;
    private readonly ICoachingIdentityReportClient _identityReportClient;

    public CoachingAdminScopeAuthorization(
        ICurrentUserService currentUser,
        ICoachingIdentityAuthorizationClient identityClient,
        ICoachingIdentityReportClient identityReportClient)
    {
        _currentUser = currentUser;
        _identityClient = identityClient;
        _identityReportClient = identityReportClient;
    }

    public async Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw Forbidden("Oturum açmış kullanıcı bulunamadı.");

        if (_currentUser.Roles.Any(role =>
                string.Equals(role, "SystemAdmin", StringComparison.OrdinalIgnoreCase)))
        {
            return CoachingAdminScope.Global;
        }

        if (!_currentUser.Roles.Any(role =>
                string.Equals(role, "InstitutionAdmin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "InstitutionOwner", StringComparison.OrdinalIgnoreCase)))
        {
            throw Forbidden("Coaching yönetim görünümüne erişim yetkiniz yok.");
        }

        var scope = await _identityClient.AuthorizeCoachingAdminAsync(userId, cancellationToken);
        if (scope is null || scope.IsGlobal || scope.InstitutionId is null || scope.InstitutionId == Guid.Empty)
        {
            throw Forbidden("Aktif kurum yönetim kapsamı bulunamadı.");
        }

        var studentIds = await _identityReportClient.GetActiveStudentIdsAsync(
            userId,
            scope.InstitutionId.Value,
            gradeLevel: null,
            cancellationToken);

        return new CoachingAdminScope(false, scope.InstitutionId, studentIds);
    }

    private static BusinessRuleException Forbidden(string message) =>
        new("Authorization.Forbidden", message);
}
