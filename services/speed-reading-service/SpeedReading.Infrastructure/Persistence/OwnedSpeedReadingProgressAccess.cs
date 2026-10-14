using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Content;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;
using SpeedReading.Infrastructure.ExternalServices;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingProgressAccess(
    OwnedSpeedReadingDbContext db,
    ISpeedReadingInstitutionDirectory institutions,
    ISpeedReadingInstitutionAdministrationAuthorization authorization,
    ISpeedReadingUserDirectory users,
    ICurrentUserService currentUser,
    IdentityProgressAccessClient globalAuthorization) : ISpeedReadingProgressAccess
{
    public async Task<SpeedReadingProgressAccessScope?> GetScopeAsync(Guid viewerUserId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || viewerUserId == Guid.Empty || currentUser.UserId != viewerUserId)
            return null;
        if (currentUser.Roles.Contains("SystemAdmin"))
        {
            var globalScope = await globalAuthorization.GetScopeAsync(viewerUserId, cancellationToken);
            return globalScope?.IsGlobal == true ? globalScope : null;
        }
        if (currentUser.User?.FindFirst("platform_product")?.Value != "speed-reading"
            || !currentUser.Roles.Any(role => role is "InstitutionAdmin" or "InstitutionOwner"))
            return null;

        var activeInstitutions = (await institutions.GetInstitutionsAsync(cancellationToken)).Institutions
            .Where(item => item.IsActive).Select(item => item.InstitutionId).ToHashSet();
        var memberships = await db.InstitutionMemberships.AsNoTracking()
            .Where(item => item.IsActive && item.Role == SpeedReadingInstitutionMemberRole.Student)
            .Select(item => new { item.InstitutionId, item.UserId }).ToArrayAsync(cancellationToken);
        var allowed = new HashSet<Guid>();
        foreach (var institutionId in memberships.Select(item => item.InstitutionId).Distinct().Where(activeInstitutions.Contains))
            if (await authorization.CanManageAsync(viewerUserId, institutionId, cancellationToken))
                allowed.Add(institutionId);
        return new(viewerUserId, false, memberships.Where(item => allowed.Contains(item.InstitutionId))
            .Select(item => item.UserId).Distinct().ToArray());
    }

    public async Task<IReadOnlyCollection<Guid>> SearchStudentUserIdsAsync(Guid viewerUserId, string searchTerm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Array.Empty<Guid>();
        var scope = await GetScopeAsync(viewerUserId, cancellationToken);
        if (scope is null)
            return Array.Empty<Guid>();
        var candidates = scope.IsGlobal
            ? await users.GetAudienceUserIdsAsync("Student", cancellationToken)
            : scope.StudentUserIds;
        var term = searchTerm.Trim();
        var matches = new List<Guid>();
        foreach (var batch in candidates.Chunk(500))
        {
            var directory = await users.GetUsersAsync(batch, cancellationToken);
            matches.AddRange(directory.Users.Where(item => item.IsActive && batch.Contains(item.UserId)
                && ($"{item.FirstName} {item.LastName}".Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (item.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)))
                .Select(item => item.UserId));
        }
        return matches.Distinct().ToArray();
    }
}
